# Task interaction-04: Claims

Source: [summary-interaction-v2.md](../summaries/summary-interaction-v2.md) §3.1, §3.2, §3.5 (G3), §4.1, §4.2, §5, §11, §13 (O1, O2), §14 N4. Depends on: [task-interaction-03](task-interaction-03-orchestration.md).

## Goal

A pressed pointer keeps its route, and one node at a time can claim it. After this task:
- `PointerClaims`, one per dispatcher, keeps the pressed pointers: their recorded paths, their claims and the click path of the mouse. It never calls behaviors;
- pointer input takes the **claim route** when the pointer is claimed, the **recorded path** while it is pressed, and the **hit path** otherwise;
- behaviors call `RequestPointerCapture` during a pass. The dispatcher resolves the requests after the pass and queues `PointerCaptureNotification` to the winner and `PointerCancelNotification` to everyone else on the path (resolve, then publish);
- every lifetime rule of §5.4 ends claims and recorded paths, including the `SetBehaviors` diff. The hover path follows the claim route while the pointer is claimed (G3).

Recognizers and `PointerInput` are N5; the behaviors that use claims are N6. N4 is tested with fake behaviors.

## Design

### 1. Types

| Type | Kind | Role |
|---|---|---|
| `PointerClaims` | internal sealed class | One per dispatcher. Pressed pointers, claims, the click path, gesture sequences; requests of the running pass; `Resolve`; the sweep (§7) |
| `PressedPointer` | internal sealed class, pooled | Pointer id, the mouse buttons held, the recorded path, the claim or null |
| `PointerClaim` | public sealed class | `Node`, `Kind`, `Pointers`, `Members`, `HasMember(object)`. Lives until it ends; its members can leave before |
| `GestureKind` | public enum | `Click = 1`, `Drag = 2`, `Pinch = 3`. Higher wins |
| `ClaimRequest` | internal readonly record struct | `(object Owner, IInteractive Node, GestureKind Kind, IReadOnlyList<int> Pointers)`, in pass order |
| `PointerCaptureNotification` | public sealed, `NotificationBase` | `PointerId`, `Claim`. Control, target route at the claim node |
| `PointerCancelNotification` | public sealed, `NotificationBase` | `PointerId`, `Claim` (null: everyone resets), `IsMember(object)`. Control, snapshot route |
| `PathSnapshot` | internal sealed class, pooled | A copy of a path and the first depth that receives. Returned to the pool when its pass ends |
| `NotificationExtensions` | public static class (N3) | Gets `RequestPointerCapture` (one pointer, several pointers) and `ReleasePointerCapture` |

`RouteKind` gets two members:

| `RouteKind` | Path | Receives | Checks during the pass | Used by |
|---|---|---|---|---|
| `Pointer` | chosen when the pass starts: claim route, recorded path or hit path | as the chosen route | as the chosen route | mouse, touch, gesture, click input |
| `Snapshot` | the copy the delivery carries | from its first receiving depth down | **none:** walked as recorded, so a node that left still receives | cancels, leaves of nodes that left |

The claim route is a `Target` route at the claim node with the input class: only the claim node receives, its ancestors only forward (Q3). The recorded path is a `Hit` route over the pointer's recorded path: it bubbles.

### 2. Classification and planning (v2 §3.1)

`NotificationClassifier` gives every pointer notification `RouteKind.Pointer`: mouse notifications, `TouchNotification`, `GestureNotification`. Other positional notifications keep `Hit`; device loss keeps control broadcast. The route itself is chosen by `PointerClaims` when the pass starts (rule 4), so input that a behavior queued sees the claims as they are when it runs.

| Notification | Pointer state | Route | Bookkeeping after the pass |
|---|---|---|---|
| `MouseDown` | not pressed | hit path | records the path (the hit path, even when nothing was hit: an empty path), adds the button, drops the click path |
| `MouseDown` | pressed (chord, O1) | claim route or recorded path | adds the button |
| `MouseMove`, `MouseWheel`, `MouseHorizontalWheel` | pressed | claim route or recorded path | |
| `MouseUp` | pressed | claim route or recorded path | removes the button. The last one: implicit release (§6 row 1), the path becomes the click path |
| `MouseClickNotification` | | the click path, kept until the next Down; hit path when there is none | |
| `TouchNotification` Down | id not pressed | hit path | records the path |
| `TouchNotification` Down | id pressed (its Up was lost) | hit path | the old press ends first: cancel to its path, claim dropped |
| `TouchNotification` Move, Up | pressed | claim route or recorded path | Up: implicit release |
| `GestureNotification` Begin | | hit path | records the path under `SequenceId` |
| `GestureNotification` other kinds | | that recorded path; hit path when there is none | End: drops it |
| any pointer notification | not pressed | hit path | |

- **A press that hit nothing is still recorded** with an empty path, so its Moves don't start hovering or hit-testing other nodes. The Up ends it.
- Gesture sequences have recorded paths but **can't be claimed** (Decision 5).
- Touch promotion (N3 §6) counts any built route: a touch on its recorded path or claim route returns true.

### 3. Requests (v2 §4.2, §5.2 step 1)

```csharp
public static bool RequestPointerCapture(this INotification notification, object owner, GestureKind kind);
public static bool RequestPointerCapture(this INotification notification, object owner, GestureKind kind, Spread<int> pointerIds);
public static void ReleasePointerCapture(this INotification notification, object owner);
```

- The node is `InteractionPass.Current.Node`, and the pointer comes from the notification's data (`PointerIds.TryGet`). The overload with `pointerIds` asks for several pointers at once (a pinch) and must include the notification's own pointer.
- **A request is valid** when:
  - it is made inside a pass that isn't a query;
  - the pass carries the notification;
  - the owner isn't null and the kind is defined;
  - each pointer is a pressed mouse or touch pointer;
  - the node is on each pointer's current route.

  A valid request is recorded in the pass and returns true. **It doesn't change the claims table.** An invalid one logs a warning and returns false.
- **Release** removes the owner from the claim of the notification's pointer. A claim ends with its last member, and the pointer falls back to its recorded path. Nobody is notified: the member chose to leave. Release is valid in the same places as a request.

### 4. Resolution (v2 §5.2 step 2)

After each pass that recorded requests, in this order:

1. Requests are taken in order of kind, highest first, and in pass order within a kind. Pass order is innermost node first, so the front-most node wins a tie.
2. A request is **granted** when every one of its pointers is free, or is held by a claim of a lower kind. Granting is all or nothing (pinch).
   - The new claim takes the request's node, kind and pointers.
   - Every later request from the same node, of the same kind, whose pointers are all in the claim, **joins** as a member.
   - Claims it replaces end, and their members leave.
3. A request whose pointers are held by a claim of the **same kind on the same node** joins that claim. This is a late join. The joining owner gets its own `PointerCaptureNotification`.
4. Every other request is rejected and changes nothing.
5. For each pointer whose claim changed, the outbox gets, in this order:
   1. a `PointerCancelNotification(pointerId, newClaim)` along a snapshot of the pointer's recorded path. Everyone who isn't a member resets, including the members of a replaced claim;
   2. then one `PointerCaptureNotification(pointerId, claim)` per new claim, on the claim route. Members publish their start when they receive it.

   A claim over several pointers sends one capture, with the pointer of its first request, and one cancel per pointer.

### 5. Delivery changes

- **Snapshot routes** skip the attachment checks of N3 §4: the node doesn't need to be attached to the root or be its predecessor's child. Each node still pushes its own local transformation, so positions stay in the space the node had.
- **The claim route** uses the input gates of a target route: display on the way, no bounds, clip or `PointerEvents`. The pointer may be far outside the claim node.
- `InteractionPass` gets the request list, pooled with the pass. A query rejects requests and releases (N3 §7).

### 6. Lifetime (v2 §5.4)

| # | Event | What happens |
|---|---|---|
| 1 | The last button or touch of a pointer goes up | After the Up pass: when the Up was processed, a `PointerCancelNotification(pointerId, claim)` goes along a snapshot of the path (§5.3 row 2). Then the claim and the record are dropped |
| 2 | A member calls `ReleasePointerCapture` | §3. Nothing is sent |
| 3 | A higher claim takes the pointer | §4 step 5 |
| 4 | Device loss | **Before** the broadcast pass: `PointerCancelNotification(pointerId, null)` along a snapshot of each affected pointer's path, then the records and claims are dropped. `MouseLost` affects the mouse and drops the click path. `LostFocus` affects every pointer. `KeyboardLost` affects none |
| 4b | Esc `KeyDown` that no receiver processed | After the key's pass: `PointerCancelNotification(pointerId, null)` along a snapshot of each claimed pointer's path, and the claims end. The pointers stay pressed and keep their recorded paths; a cancelled recognizer needs a new Down (Decision 11) |
| 5 | A node on a pressed pointer's path leaves | The sweep, §7 |
| 6 | A behavior is removed by `SetBehaviors` | §8 |
| 7 | A member that isn't a behavior of the node | Rows 1–5 only |
| 8 | The root is disposed or gets a parent | The dispatcher is dropped with its table (N3 §9). No notifications |

### 7. The sweep (rows 5, hover)

A node **is reachable** when the hit test could reach it: it is attached under the root, laid out, displayed and not `PointerEvents.None`, and so is every ancestor. The sweep checks the pressed pointers and the hover path against the tree as it is now:

```
per pressed pointer
  valid = the longest prefix of the recorded path whose nodes are reachable, each the parent of the next
  claim, and the claim node is in valid:
      the path is cut to valid; the nodes cut off get a cancel (null), snapshot from the first one cut
  claim, the claim node is reachable but not in valid (moved inside the root):
      the path becomes root … claim node; the old nodes not on it get a cancel (null)
  claim, the claim node is not reachable:
      cancel (null) along the whole old path, snapshot; claim and record dropped
  no claim:
      as the first case
hover path
  cut to its valid prefix; the nodes cut off get their leave, deepest first, on snapshot routes
```

- Cancels and leaves from the sweep run as passes **before** whatever triggered the sweep, so the next input sees the tree without the nodes that left.
- They use the caller of the last notification the root received.
- **The sweep runs at the start of the root's `Render` and before every notification the root receives** (Decision 10). `Renderable.Render` gets a protected virtual hook at its start, and `Interactive` overrides it for a root that has a dispatcher. Without pressed pointers and hover, the sweep returns at once.

### 8. `SetBehaviors` diff (row 6)

- When the node's root has a dispatcher with pressed pointers, `SetBehaviors` compares the old and new behaviors by reference.
- Each removed behavior leaves every claim immediately. A claim left with no members ends like row 5: a cancel (null) along its pointer's path. The recorded path is kept.
- A removed behavior on a node of a pressed pointer's path gets a **direct** `PointerCancelNotification(pointerId, null)`. It runs inside a control pass, with the node as the acting node, so calls the behavior makes from it are checked like any other. It is delivered by the sweep, which holds the removed behaviors until then.
- A replaced instance (patch reload) is a removal plus an addition. The new instance starts idle.

### 9. Hover while claimed (G3)

`HoverTracker` (N3 §5) takes:
- the claim route while the pointer is claimed;
- the hit path otherwise, which is an extra hit test while the pointer is pressed and its input follows the recorded path.

So a press that drags off a node leaves it until a claim takes the pointer, and a claimed drag keeps hovering its claim node.

## Decisions (settled)

1. **A request returns true when it is recorded,** not when it is granted. The grant comes as `PointerCaptureNotification`.
2. **Cancel before capture.** The losers reset before the winners publish their start: a child's drag ends before an ancestor's pinch starts.
3. **Late joins** of the same kind on the claim node are granted. Other requests against an existing claim of the same or a higher kind are rejected.
4. **A request names pointers on whose route its node is.** A node can't claim a pointer that was pressed somewhere else.
5. **Gesture sequences aren't claimable.** Windows sends `WM_GESTURE` only to windows that don't take touch, so they never mix with touches. A request with a gesture notification is rejected with a warning.
6. **A claim survives a move inside its root.** A claim node reparented under the same root keeps its claim; its recorded path is rebuilt. The nodes that dropped off the path get a cancel.
7. **A node leaving below the claim node doesn't end the claim.** Only the nodes cut off are cancelled. A canvas pan keeps going when the item under the finger is removed.
8. **One capture per claim; one cancel per pointer.**
9. **A press that hit nothing is recorded with an empty path.** Its Moves don't hit-test into nodes until it is released.

10. **The sweep, not hooks, ends claims of nodes that left** (§7). It runs at the root's next `Render` and before every notification. One check covers detach, hide, dispose and `PointerEvents = None`, on the node or any ancestor, whether a setter or a style made the change. No behavior runs inside `SetChildren`, `Dispose` or a style pass, and a reparent within one frame isn't a leave. The cancel arrives at most one frame late, after the node has left, so snapshot routes walk the path as recorded. This replaces v2 §5.4's "queued before the node leaves".
11. **Esc cancels only when no receiver processed its `KeyDown`.** A text field or a patch that uses Esc keeps it.

## Tests

`VL.Fu.Tests\Core\Interaction\ClaimsTests.cs` (new), on the N3 scene, with fake behaviors that log, can request or release on chosen notifications, and record what they receive:

- **Routes.** A Move while pressed follows the recorded path off the node. A second mouse button follows the existing route (O1). The last Up releases. `MouseClickNotification` takes the Up's path. A press on nothing delivers its Moves to nothing. Gesture Begin records; later kinds follow; End drops.
- **Requests.** A request outside a pass, from a query, with a foreign notification, for a pointer that isn't pressed or not on the node's route, or with a gesture: false, with a warning. A valid request returns true and changes nothing until the pass ends.
- **Resolution.** Innermost wins on equal kinds. A higher kind on an ancestor replaces a child's claim: cancel to the child's members, then capture to the ancestor. Two requests from the same node and kind both become members. A late join gets its capture. A pinch over a pointer held by an equal claim is rejected whole. Cancel comes before capture in the outbox.
- **Claim route.** Input reaches only the claim node, even outside its bounds; ancestors don't receive it. Hover follows the claim node (G3).
- **Lifetime.**
  - A processed Up sends a cancel along the path; an unprocessed one doesn't. Release falls back to the recorded path.
  - `MouseLost` cancels before its broadcast and drops the click path. `LostFocus` cancels every pointer. An unprocessed Esc cancels the claims and keeps the pointers pressed; a processed one changes nothing.
  - Sweep: the claim node is detached, hidden through an ancestor, set to `PointerEvents.None`, or disposed; each one gets its cancel, and its behaviors receive it.
  - A node below the claim node is removed and the claim stays. A claim node reparented in the same root keeps its claim.
  - A hovered node detached gets its leave.
- **`SetBehaviors` diff.** A removed member leaves the claim; the last one ends the claim. A removed behavior on a pressed path gets one direct cancel. Unchanged behaviors get nothing.
- **Outbox.** No capture or cancel is delivered inside another pass.

`DispatcherTests` and `DeliveryTests` keep their expectations. Their probes ignore capture and cancel, the way they ignore enter and leave.

## As built

Where the implementation differs from the design above:

* `RequestPointerCapture` takes the pointers as `IReadOnlyList<int>`, not `Spread<int>`. A Spread still binds, since it implements the interface.
* Removals found by the `SetBehaviors` diff are recorded and handled at the next sweep, not inside `SetBehaviors`.
* `PressedPointer` and path snapshots are not pooled yet.
* The sweep cuts a recorded path to its valid prefix instead of dropping the press. A claim whose node left gets one cancel along the whole old path, then the path is cut.
* Pinch: lifting one finger removes that pointer from the claim. The claim ends when no pointer is left.
* A request is valid when the node is on the pointer's recorded path, or on the pass's hit path for the press itself.
* `ReleasePointerCapture` sends no notifications. The pointer falls back to its recorded path.
* Cancels and leaves sent by the sweep carry a null sender and `Keys.None`.
* Gesture paths are cut silently: gestures can't be claimed, so nobody waits for a cancel.
* `Renderable` got a `protected virtual OnRenderingAsRoot()` hook; `Interactive` uses it to sweep once per frame.
* `DeliveryTests`: three tests pressed several times without an Up. With N4 the later presses follow the recorded path, so they now release between presses.

## Deferred

* `PointerInput.TryRead`, recognizers: N5.
* Fu behaviors reacting to capture and cancel (`Clickable`'s `IsPressed` rule, O2): N6.
* VL node wrappers for the extensions get their pins when N6 patches them.
* Focus routing for keys.

## Risk

* A disposed node may still receive a cancel on a snapshot route. `Forward` must not touch its freed native handle: it uses the stored local transformation, and the layout gate doesn't apply to control.
* The extra hit test per Move while the pointer is pressed (§9) costs one hit test of O(depth × siblings).
* A behavior that requests on every Move of a claimed pointer without being a member is rejected silently each time. That is cheap, but a patch author gets no hint why the drag never starts.
