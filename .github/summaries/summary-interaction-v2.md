# Interaction v2: notification-native design

**Status:** sealed. This is the interaction plan; it supersedes [summary-interaction.md](summary-interaction.md) (v1). The review feedback R1–R8 (§12) and the decisions Q1–Q6 and O1–O5 (§13) are incorporated.

v1 stops VL notifications at the Fu root and replaces them with a private pipeline: `PointerEvent`, `PointerEventsProvider`, and recognizers called by the provider. v2 keeps VL's notification flow: **a Fu node is a VL layer in `Notify` as it is in `Render`.** Layers handle the notifications they understand, behaviors handle theirs, and custom notifications pass through. Fu adds only what VL lacks:
- pointer routes;
- claims and capture;
- a thin per-root orchestrator that runs one delivery pass at a time;
- a few notifications of its own.

What v2 keeps from v1:
- the tier split: recognizers, headless behaviors, scopes, manipulators;
- the §6.1 channel table and cancel writes;
- the §7 math;
- the selection and canvas semantics (C1–C8, S1–S8);
- the code rules in §8, except where this document says otherwise.

Sources:
- VL.Skia `ILayer` and `IBehavior` (`VL.Core.Skia\Interfaces.cs`);
- `SetBehavior` (`VL.Skia\src\Layers.cs`);
- `TransformUpstream` and `IntermediateTransformNotificationSender` (`VL.Core.Skia\Transform.cs`);
- `Group` (`VL.Skia\src\Group.cs`);
- `SkiaRenderer.OnNotification`;
- `CallerInfo` (`VL.Core.Skia\CallerInfo.cs`);
- `VL.Core\src\IO\Notifications\*.cs`.

---

## 1. What VL already does

| VL convention | Where | v2 keeps it as |
|---|---|---|
| A layer notifies upstream first and runs its own logic only if upstream didn't process the notification | `ILayer.Notify` doc, `SetBehavior.Notify` | A node offers each notification to its children first, then its `Layer`, then its behaviors |
| `Group` asks the front input first and stops at the first `true` | `Group.Notify` | Children are visited front to back, through the same `OrderedChildren` as `Render` |
| A transform layer pushes its matrix onto `CallerInfo` and wraps the notification with a sender whose `MapFromPixels` maps into the new space | `TransformUpstream.Notify` | Each node wraps with `FuNotificationSender`, so `PositionInWorldSpace` is in the node's own space for everything inside it |
| A behavior is anything with `bool Notify(INotification, CallerInfo)` | `IBehavior` | `SetBehaviors(Spread<IBehavior>)`, which takes Fu's C# behaviors and patched behaviors alike |
| `true` means processed; the renderer stores it in `Handled` | `SkiaRenderer.OnNotification` | Same. Touch promotion still relies on it (v1 TP) |

Facts that limit the design:
- **Senders don't survive native layers.**
  - `IntermediateTransformNotificationSender` is internal and keeps the original notification privately.
  - A `TransformUpstream` inside a node's `Layer` therefore hides `FuNotificationSender` from everything below it.
  - `CallerInfo` is a record with no extension slot (only `RenderInfoHack`). Node identity can't travel with the notification or the caller (R6, §4.2).
- **`MouseLostNotification` derives from `MouseNotification`** and has a position. Classifying by "has a position" alone would hit-test a device loss (R7, §3.1).
- **`GestureNotification` sequences** (Begin … End) carry a `SequenceId`. They are tracked like a pressed pointer.
- **Channel writes are synchronous.** Subscribers may mutate the tree, swap behaviors, or raise notifications in the middle of a delivery (R5, §4.1).

What VL doesn't have, and v2 adds:
- **Occlusion.** `Group` falls through to the next input when the front one returns false. Fu nodes are boxes: a pointer over a node doesn't reach the siblings behind it (G1, decided).
- **Implicit and explicit capture.** A pressed pointer keeps its route, and behaviors can claim it.
- **Claims between behaviors,** resolved once per pass: ancestor drag over child click, pinch over drag (v1 §5.3).

---

## 2. Architecture

**A notification is delivered in a pass. The orchestrator classifies it and chooses its route. Nodes forward it along the route the way layers do, and behaviors decide for themselves. The orchestrator resolves claims after the pass, then delivers what the pass produced.**

| Concern | Owner | Kind |
|---|---|---|
| Forwarding: the transform push, sender wrap, child order, gates | `Interactive<TSelf>.Forward`, mirroring `Renderable.Render` | node base |
| Classification, route choice, running passes, the outbox | `NotificationDispatcher`, one per root, created lazily | internal class |
| What a running pass knows: the notification, delivery class, route, the node stack, claim requests | `InteractionPass` | internal class, ambient while running |
| Pressed pointers: recorded paths, claims and their members | `PointerClaims` | internal class, no callbacks into behaviors |
| The hover path and its diff | `HoverTracker` | internal class |
| The node and spaces for whoever receives a notification | `FuNotificationSender : IWorldSpace2d, IProjectionSpace` | public class, spaces only |
| What happened | recognizers, called by their own behavior | internal classes |
| Where it goes in the patch | behaviors: any `IBehavior`; Fu's are `Hoverable`, `Clickable`, and so on | VL nodes |
| Fu's own events | `PointerCaptureNotification`, `PointerCancelNotification`, `PointerEnterNotification`, `PointerLeaveNotification`, `SelectionNotification`, `GroupManipulationNotification` | public `NotificationBase` subclasses |

### 2.1 Is this an orchestrator again?

Yes, but a thin one, and it doesn't replace VL's flow.

| | v1 `PointerEventsProvider` | v2 `NotificationDispatcher` |
|---|---|---|
| Input | Converts VL notifications into a private `PointerEvent` | Delivers VL notifications unchanged |
| Calls behaviors | Directly, through a `Behavior` base | Never. Nodes forward, and behaviors are reached as VL behaviors |
| Layer content and patched behaviors | Don't see anything | See everything on the route |
| Resolution | Candidates, then publish | Claim requests collected during the pass, then resolved, then `PointerCapture` / `PointerCancel` notifications |
| Custom notifications | Stop at the root | Pass through on the broadcast route |

### 2.2 One pass

```
SkiaRenderer -> root.Notify(notification, caller)                  public ILayer.Notify: the only external entry
  dispatcher.Run(notification, caller)
    if a pass is already running on this root: enqueue, return false   (R5)
    pass = InteractionPass.Begin(notification, caller)                 ambient for the duration (R6)
    1. classify (§3.1): delivery class and route
    2. root.Forward(notification, caller, pass)                        the same body on every node (below)
    3. resolve claim requests (§5.2)
    4. bookkeeping: the recorded path at Down, implicit release after Up (§5.4), the hover diff (§3.5)
    5. drain the outbox: each queued notification runs as its own pass, first in, first out
       (PointerCapture, PointerCancel, Enter/Leave, Dispatch, re-entrant input)
    pass.End()
    return the handled value of the original pass

node.Forward(notification, caller, pass)
  gate for the delivery class (§3.2)
  inner   = caller.WithTransformation(caller.Transformation · LocalTransformation)
  wrapped = notification.WithSender(new FuNotificationSender(this, inner, ...))
  pass.Push(this)                                                    node stack, for capture calls from inside the Layer (R6)
  1. route children, front to back -> child.Forward(notification, inner, pass)
  2. Layer?.Notify(wrapped, inner)
  3. every eligible behavior: behavior.Notify(wrapped, inner)
  pass.Pop()
  input class: stop at the first true in 1 and 2; true if any behavior returns true
  control and fan-out classes: visit everything, ignore the results
```

v1's `PointerEvent`, `PointerEventsProvider`, `IGestureRecognizer.Publish`, the `Behavior` base class and `IGroupManipulable` are not needed.

---

## 3. Routes and delivery

### 3.1 Classification (R7)

The classification goes by notification type. Only notifications with no pointer meaning use the broadcast route.

| Notification | Class | Route |
|---|---|---|
| `MouseDown/Up/Move/Wheel/HorizontalWheelNotification`, `TouchNotification` | input | The pointer is claimed: **claim route** (root down to the claim node). Pressed and unclaimed: **recorded path**. Otherwise: **hit path** at its position |
| `MouseClickNotification` (injected by VL's `Mouse`) | input | The path of the Up that preceded it, kept until the next Down |
| `GestureNotification` | input | `GestureBegin`: hit path, recorded under its `SequenceId`. Later kinds: that recorded path. `GestureEnd` releases it |
| Any other `INotificationWithPosition`, e.g. custom positional notifications from patches | input | Hit path at its position, with no press tracking |
| `MouseLostNotification`, `LostFocusNotification`, `KeyboardLostNotification` | control | Broadcast to everything, after the device loss cancels the affected claims and pressed pointers (§5.3) |
| `KeyDown/Press/UpNotification`, `GotFocusNotification`, any other notification | input | **Broadcast,** VL `Group` semantics: every child front to back until one returns true. Focus routing comes later |
| `PointerCaptureNotification`, `PointerCancelNotification` | control | The claim members, or the pointer's recorded path (§5) |
| `PointerEnterNotification`, `PointerLeaveNotification` | control | **Target:** one node; its ancestors only forward |
| `SelectionNotification`, anything sent with `Dispatch` | fan-out | **Subtree** of the dispatching node |
| `GroupManipulationNotification` | fan-out (`Begin`), control (`Apply`, `End`, `Cancel`) | `Begin`: the provider's subtree. The rest: the transaction members (§7.2) |

Mouse buttons follow browser pointer semantics: the mouse is **one pointer** (id −1). A second button pressed while one is held is a chorded change of the same pointer: it follows that pointer's current route and doesn't start a new hit test (O1).

### 3.2 Delivery classes (R3)

| Class | Gates at each node | A child or the `Layer` returns true | Behaviors | Return value |
|---|---|---|---|---|
| **Input** | Display everywhere; also bounds, `ClipArea` and `PointerEvents` on the hit path | Stops the rest of the node: VL semantics | All eligible ones, even after one returns true | Handled: true if anyone processed it |
| **Control** | None. Reaches hidden nodes, `PointerEvents = None` nodes, and nodes about to detach | Ignored | All | Ignored |
| **Fan-out** | Display for `Begin` and `Dispatch` | Ignored | All | Ignored. A query reads its accumulator (§4.3) |

- Control notifications are guaranteed to reach every node on their route. A `Layer` or a behavior that answers true can't block a cancel, and a node hidden mid-press still gets its cancel and its leave.
- **Eligible behaviors:** the node's current `Behaviors`, minus Fu behaviors that are disabled.
- **The claim route** reaches only the claim node; the ancestors on it only forward. Every eligible behavior on the claim node sees the captured pointer's input, members and non-members alike. A non-member on that node has already been cancelled, and uses the input only to finish its own pending feedback, e.g. `Clickable` clears `IsPressed` on the Up (O2).
- The `Layer` sees control and fan-out notifications too, so VL content and patched behaviors inside it can reset on a cancel.

### 3.3 Order and "processed"

- Each node forwards inward first: route children, then `Layer`, then behaviors. The deepest node acts first and its ancestors after it, like a bubbling event.
- For input, a `true` from a child or from the `Layer` stops the node's own layer and behaviors (VL semantics). A patch that processes a notification in a node's layer keeps it from Fu's behaviors, which is what the patch asked for.
- **All eligible behaviors on one node see the notification,** even after one returns true (decided). They belong to the same node, like `Clickable` and `Selectable` in v1.
- Fu behaviors return true only when they finish or hold something exclusively: a click fired on Up, input of a granted drag, a handled wheel. While a gesture is pending, or a claim has only been requested, they return false so the notification still reaches ancestors, which may request a stronger claim on the same event.

### 3.4 Sender, spaces and identity (R6)

`FuNotificationSender` is created per node per notification, like VL's `IntermediateTransformNotificationSender`. **It carries spaces, not identity.**

| Member | Meaning |
|---|---|
| `Node` | The `IInteractive` that forwarded the notification. A convenience for code that receives the Fu sender directly |
| `WindowToNode`, `WindowToParent` | The current inverses for that node: `SKMatrix` internally, public as Stride `Matrix` (D6) |
| `Scaling`, `TimeMs` | From the root: `CallerInfo.Scaling` and the root's stopwatch |
| `MapFromPixels` (`IWorldSpace2d`) | Maps a window position into node space, so `PositionInWorldSpace` works as in any VL layer |
| `MapFromPixels` (`IProjectionSpace`) | Forwarded from the original notification, as VL does |

- **Identity comes from the running pass, not from the sender.**
  - `InteractionPass.Current` is set by the dispatcher for the duration of a pass. It is thread-static and kept as a stack, because a Fu root nested inside another root's `Layer` runs its own pass.
  - `pass.Node` is the node whose `Layer` or behaviors are running right now.
  - Capture calls and `Dispatch` from anywhere inside a node, including below a native `TransformUpstream` in its `Layer`, resolve the node and the pointer from the pass.
- **Pointer ids come from the notification data,** which `WithSender` copies unchanged: the mouse is −1, a touch uses `TouchNotification.Id`, and a gesture uses `GestureNotification.SequenceId`.
- Recognizers freeze `WindowToParent` at press (A1), so a drag offset stays correct while the node moves. Behaviors attached to a node always receive the Fu sender. Content deeper in the `Layer` uses VL's spaces, which are correct for that content.

**Allocation:** a pass allocates one sender and one wrapped notification per node on the route, and `CallerInfo.WithTransformation` allocates as well. VL's transform layers do the same, and routes are O(depth). The v1 rule "no allocation per event" is relaxed to "no allocation inside recognizers and behaviors". `InteractionPass` objects and the outbox are pooled per root.

### 3.5 Hover

- `HoverTracker` keeps the last hover path: the hit path of the mouse or primary touch, or the claim route while the pointer is claimed (G3).
- After each pointer pass, it compares the new path with the last one. Nodes that left get a `PointerLeaveNotification` and nodes that entered get a `PointerEnterNotification`, through the outbox (control, target route).
- A node that is hidden or detached while hovered gets its `PointerLeave` before it leaves (control delivery).
- `Hoverable` writes `IsHovered` from those notifications. A patched behavior can do the same.
- Hover is not press state: `PointerCancel` doesn't change it.

---

## 4. Orchestration

### 4.1 Passes and the outbox (R5)

Copying the route protects the iteration, but it doesn't stop a channel subscriber from:
- raising a notification while one is still being delivered;
- cancelling a behavior that is still on the route of the current notification;
- changing the claims table during resolution.

The rules:
1. **One pass at a time per root.** `Run` called while a pass is running enqueues the notification and returns false. This covers re-entrant input as well as Fu's own notifications.
2. **Everything a pass produces is queued:** claim results (`PointerCapture`, `PointerCancel`), Enter and Leave, `Dispatch`, the cancels that detach and hide raise. Nothing is delivered in the middle of another delivery, so an ancestor never sees the rest of a Move after it was cancelled earlier in that same Move.
3. **The outbox drains first in, first out** before `Run` returns, each entry as its own pass. A drain that runs more than 64 passes for one external notification logs a warning and drops the rest, as a guard against notification loops between behaviors.
4. **Routes are snapshots.** The route is a pooled array, filled when the pass starts. Before forwarding, each node checks that it is still attached to the same root; a node that was detached during the pass is skipped. Control notifications that a detach or hide raises are queued before the node leaves, so they still reach it (§5.4).
5. **Transforms can be stale within a pass.** A behavior that moves a node during a pass doesn't change the matrices of that pass. The next pass picks up the change. Recognizers freeze their mapping at press anyway (A1).

### 4.2 Pass context (R6)

`InteractionPass` is internal. Public calls reach it through the ambient current pass:

```csharp
// Extensions on INotification; matching VL nodes give patches the same calls.
public static bool RequestPointerCapture(this INotification notification, object owner, GestureKind kind);
public static void ReleasePointerCapture(this INotification notification, object owner);
public static void Dispatch(this INotification notification, INotification toDispatch);
```

- `notification` identifies the pointer: its id comes from the notification data (§3.4). The node is `InteractionPass.Current.Node`.
- A call made outside a pass, or with a notification that doesn't belong to the current pass, returns false (or does nothing) and logs a warning. It never throws.
- `Dispatch` sends into the subtree of the current node. It is queued (rule 2 above).

### 4.3 Synchronous queries

Some fan-out notifications are questions, not events. The provider needs the answer during its own `Notify`. One example is `SelectionNotification` with the `CollectBounds` action (S8).

- `Query(INotification)` delivers into the current node's subtree immediately, as a nested fan-out inside the running pass, and returns when every node has seen it.
- Handlers of a query **only add to the notification's accumulator.** They must not write channels, request capture or dispatch. Under a query, the pass rejects those calls with a warning, so a query can't start a cascade.
- Queries are the only exception to rule 2 of §4.1.

---

## 5. Claims and capture

### 5.1 Claims (R1)

A capture is a **claim**. Coexistence is part of the claim model, not an exception to it.

| Field | Meaning |
|---|---|
| `Node` | The node the claim belongs to. The claim route ends there |
| `Kind` | `GestureKind`: `Click = 1`, `Drag = 2`, `Pinch = 3` |
| `Pointers` | The pointer ids it holds. A pinch holds two |
| `Members` | Owners that joined (compared by reference): behaviors on `Node`, or VL content in its `Layer` |

How gestures coexist:

| Case | How it works |
|---|---|
| Two behaviors of the same kind on the same node (`Draggable` and a patched drag behavior) | The second request **joins** the claim. Both receive the grant and the input |
| A click and a drag on the same node (`Clickable` or `Selectable` next to `Movable`) | The click never requests. When the drag is granted, the click isn't a member and gets `PointerCancel`, so the drag suppresses `OnClick` as in v1. Because the claim is on its own node, `Clickable` keeps `IsPressed` until the Up (O2) |
| A click on a node, and a drag claimed by another node (an ancestor pan, a descendant drag) | The click gets `PointerCancel` from a claim on another node: it suppresses `OnClick` and clears `IsPressed` at once (O2) |
| `Hoverable` next to anything | Hover is not press state. Cancel and capture don't touch it (§3.5) |
| Two fingers dragging two different nodes | Separate pointers, separate claims |
| A pinch on an ancestor while a child drags one of the fingers | The pinch requests both pointers with a higher kind. The child's drag claim is replaced, and its members get `PointerCancel` |
| A pinch requested while one of its pointers is held by an equal or higher claim | All or nothing: the whole request is rejected |
| Mouse right-button pan on the canvas while a left-button drag is active | One pointer (§3.1): the right button follows the existing claim route and doesn't reach the canvas. Each button starting from idle works as expected (O1) |

### 5.2 Request, resolve, grant (R2)

v2 revision 1 said "capture, then publish". A behavior that captured first would publish its start before an ancestor with a higher kind, receiving the same event later in the same pass, took the pointer away: `IsDragging` true then false, `OnDragStart` then `OnDragEnd`, all within one Move. Revision 2 restores v1's F5, **resolve, then publish**:

1. During the pass, behaviors call `RequestPointerCapture`. The request is recorded in the pass and **doesn't change the claims table.** The behavior returns false, so the event keeps bubbling and ancestors can request too.
2. After the pass, the dispatcher resolves, per pointer:
   - an existing claim stays unless a request has a **higher** kind;
   - among the new requests, the highest kind wins; on equal kinds the first request in pass order wins, which is the innermost node, so the front-most node wins;
   - requests from the winner's node with the winner's kind join as members;
   - multi-pointer requests are all or nothing.
3. Through the outbox:
   - `PointerCaptureNotification(pointerId, claim)` goes to the members of the new claim (control route). **Members publish their start when they receive it.**
   - `PointerCancelNotification(pointerId, claim)` goes along the pointer's recorded path. Everyone who isn't a member resets, including the members of a replaced claim.
4. Requests that lost get nothing more than that cancel. Rejected requests from behaviors that already hold the pointer through another claim change nothing.

Pending feedback isn't a claim (O2):
- `Clickable` writes `IsPressed` on Down, immediately.
- If a drag on the **same node** wins later, the cancel suppresses `OnClick`, and `IsPressed` stays true until the Up, because the user is still pressing that node.
- If a claim on **another node** wins, the cancel suppresses `OnClick` and clears `IsPressed` at once. That matches the platform convention: a button looks pressed until a scroll takes over.

`PointerCancelNotification.Claim.Node` tells the two cases apart. A behavior compares it with the node it pressed on.

### 5.3 `PointerCancelNotification`

Every behavior that took part in that pointer resets, unless it is a member of `claim`:
- behaviors that published something write their idle facts (v1 B7);
- behaviors that never published stay silent;
- the one exception is `IsPressed` under a claim on the same node (§5.2, O2). It is cleared on the Up, not by the cancel.

| Source | `claim` |
|---|---|
| A claim is granted (§5.2) | the new claim |
| A handled Up | the claim, or null if none. This resets ancestors whose Up never arrived because an inner node returned true, e.g. an ancestor `Clickable` still showing `IsPressed` |
| Esc, `MouseLostNotification`, `LostFocusNotification` | null: everyone resets. Device losses also drop the pressed pointers of that device |
| The claim's lifetime ends (§5.4) | null |
| `SetEnabled(false)`, a channel swap | not sent: the behavior cancels itself (v1 F6, F7) |

### 5.4 Owner lifetime (R4)

The claims table holds references for as short a time as possible. A claim ends at the first of these:

| # | Event | What happens |
|---|---|---|
| 1 | The pointer's last button or touch goes up | **Implicit release** after the Up has been delivered to the members. Patched behaviors don't need to call release |
| 2 | A member calls `ReleasePointerCapture` | The member leaves the claim. The claim ends with its last member. The pointer falls back to its recorded path |
| 3 | A higher claim takes the pointer | The old members get `PointerCancel` (§5.2) |
| 4 | Device loss: Esc, `MouseLost`, `LostFocus` | `PointerCancel` to everyone on the path |
| 5 | The claim node leaves: detached, hidden, disposed, `PointerEvents` set to None | `PointerCancel` is queued along the path before the node leaves (control delivery reaches it, §3.2). Then the claim and the recorded paths through that node are dropped |
| 6 | A member leaves its node: removed from `SetBehaviors`, or replaced by a new instance after a patch reload | `SetBehaviors` diffs the old and new spread. A removed behavior gets a direct `PointerCancel` call before it is dropped, and is removed from every claim. A claim with no members left ends as in 5 |
| 7 | A member that is not a behavior of the node (VL content in the `Layer`) | Its lifetime is bound to the node: rows 1–5 only. Fu can't see it leave the `Layer` |
| 8 | The root is disposed | The table is cleared. No notifications |

The recorded path of a pointer that has no claim follows rules 1, 4 and 5 the same way.

---

## 6. Behaviors

### 6.1 Contract

- Behaviors are plain `IBehavior`. There is no Fu base class; `SetBehaviors(Spread<IBehavior>)` (pin order 25) takes Fu's behaviors and patched ones alike.
- A Fu behavior:
  - decodes pointer notifications with `PointerInput.TryRead(notification, out PointerSample sample)`. The sample holds the kind, pointer id, buttons, modifiers, window position, the sender's matrices, `Scaling` and `TimeMs`, decoded once per behavior by one shared switch;
  - feeds its own recognizers and reads their state synchronously;
  - **requests** a claim when its recognizer starts, and publishes the start when it receives `PointerCaptureNotification` (§5.2);
  - resets on `PointerCancelNotification` unless it is a member of the claim;
  - writes channels.
- Recognizers keep v1's state machines: an enum and a `switch`, idle field initializers, and `Cancel()` returns to idle. `Process` returns the new state directly. They don't use `IObservable`; public `IObservable<TState>` outputs remain a later option.
- `SetEnabled` is on each Fu behavior. A small internal helper holds the shared compare-and-cancel code; there's no base class.
- One instance per node is a convention, as for VL behaviors on layers. During a gesture, a Fu behavior ignores input whose `Node` isn't the node it pressed on.

```csharp
public sealed class Draggable : IBehavior
{
    private readonly DragGestureRecognizer _drag = new();
    private IChannel<Vector2>? _offset;
    private IChannel<bool>? _isDragging;
    private IChannel<Unit>? _onDragStart;
    private IChannel<Unit>? _onDragEnd;
    private bool _enabled = true;
    private bool _granted;

    public bool Notify(INotification notification, CallerInfo caller)
    {
        if (!_enabled)
            return false;

        switch (notification)
        {
            case PointerCaptureNotification capture when capture.Claim.HasMember(this):
                return OnGranted();
            case PointerCancelNotification cancel when !cancel.IsMember(this):
                return OnCancelled();
        }

        if (!PointerInput.TryRead(notification, out var sample))
            return false;

        var drag = _drag.Process(in sample);
        switch (drag.Phase)
        {
            case DragPhase.Started:
                notification.RequestPointerCapture(this, GestureKind.Drag);
                return false;   // ancestors still see this Move and may request a stronger claim
            case DragPhase.Moved when _granted:
                _offset?.EnsureValue(drag.OffsetInParent);
                return true;
            case DragPhase.Ended when _granted:
                _offset?.EnsureValue(drag.OffsetInParent);
                _isDragging?.EnsureValue(false);
                _onDragEnd.Fire();
                _granted = false;
                return true;    // the dispatcher releases the claim after this Up (§5.4 #1)
            default:
                return false;   // Idle, Pending, or waiting for the grant
        }
    }

    private bool OnGranted()
    {
        _granted = true;
        _offset?.EnsureValue(_drag.Current.OffsetInParent);
        _isDragging?.EnsureValue(true);
        _onDragStart.Fire();
        return true;
    }

    // Writes the idle facts if the drag was granted (B7), then resets the recognizer.
    private bool OnCancelled() { /* ... */ return false; }
}
```

`RequestPointerCapture`, `ReleasePointerCapture`, `Dispatch`, `Query` and `Fire` are extensions over `INotification` and `IChannel<Unit>`. Matching VL nodes give patched behaviors the same calls.

### 6.2 Tier 1

Same channels, settings and cancel writes as v1 §6.1, without `OnDoubleClick`. Differences:
- `Hoverable` reacts to `PointerEnterNotification` and `PointerLeaveNotification`.
- `Clickable` returns true on the Up that fires `OnClick`. The `PointerCancel` that follows a handled Up resets an ancestor `Clickable` that was also pressed.
- `Clickable` under a cancel: `OnClick` is suppressed either way. `IsPressed` is kept until the Up when the claim is on its own node, and cleared at once otherwise (O2).
- `Pinchable` requests every pointer it tracks with `GestureKind.Pinch` (all or nothing, §5.1).
- Wheel: the hit path bubbles from the deepest node, and the first behavior that returns true for the wheel consumes it. That is v1's "front-most node with a wheel behavior", with no special case.

---

## 7. Selection and group manipulation

The providers talk to their items through notifications instead of walks and interfaces, so patched items can take part too.

### 7.1 Selection

| v1 | v2 |
|---|---|
| `Selectable` finds its provider by walking up (S1) | Same: walk up from the pass node and find the nearest node with a `SelectionProvider` behavior |
| The provider walks its subtree to clear or select (S3) | The provider dispatches `SelectionNotification(provider, action, target, rect, modifiers)` into its subtree. `action` is `Clear`, `Replace`, `Add`, `Toggle` or `Marquee`. Each `Selectable` acts only if its nearest provider is `provider`, which also hides the items of nested providers (S3) |
| The provider computes the bounds by walking the selected items (S8) | The provider sends `SelectionNotification(action = CollectBounds)` as a **query** (§4.3). Selected items add their box, mapped into provider space through the sender matrices, to the notification's accumulator (decided) |

### 7.2 Group manipulation: transactions (R8)

Dispatching `Apply` into the subtree and letting "the selected items" act is not enough. Selection can change in the middle of a drag:
- an item is deselected through its channel;
- a new item gets selected;
- an item is hidden or detached.

Without a fixed set of members, items would get an `Apply` without a `Begin`, or a `Begin` without an `End`.

`GroupTransaction` is created by the provider when a group drag is granted. It holds:
- the provider;
- the frozen provider-to-parent mapping per member (v1 §7.2);
- the **members**.

| Phase | Delivery | Who acts |
|---|---|---|
| `Begin` | Fan-out into the provider's subtree | Items that are selected, whose nearest provider is the transaction's provider, and that are displayed. They call `transaction.Join(owner)` and freeze their value at press |
| `Apply(deltaInProvider)` | Control, members only | Members only. Items selected after `Begin` wait for the next transaction |
| `End` | Control, members only | Members, including ones deselected or hidden since `Begin` |
| `Cancel` | Control, members only | Members restore the value they froze at `Begin` |

- A member that leaves during a transaction (detached, disposed, or removed from `SetBehaviors`) **keeps its latest value** and stops participating. It gets a direct `End` for itself, so it writes its idle facts, and is removed from the members. The remaining members continue (O5).
- That `End` comes before the generic lifetime cancel of §5.4. The later cancel no longer concerns the transaction, so it doesn't restore the value.
- The provider is cancelled (`PointerCancel`, `SetEnabled(false)`, the provider node leaves): the whole transaction is cancelled.
- `GroupManipulationNotification(transaction, phase, deltaInProvider)` carries the transaction, so a member can tell its own transaction from a nested provider's.
- This replaces `IGroupManipulable`. `Movable` and `Transformable` items apply the delta with v1 §7.2.

---

## 8. Canvas

`CanvasProvider` is unchanged from v1 §6.3 and S4:
- the right button pans; the wheel and a pinch zoom;
- the content keeps its own `TransformOrigin`, and the provider compensates with v1 §7.4;
- the wheel arrives through the normal hit path, so items inside the canvas that consume the wheel (a scroll view) win, because they are deeper;
- a pan requests a `Drag` claim and a zoom pinch requests a `Pinch` claim, like any other behavior.

---

## 9. v1 decisions in v2

| v1 | v2 |
|---|---|
| F1 typed states, `Gesture.cs` deleted | Kept. States are returned by `Process` instead of pushed |
| F2 `PointerEvent` decoded once at the root | Replaced by `PointerInput.TryRead`, once per Fu behavior. VL notifications travel unchanged |
| F3 mutable recognizers | Kept |
| F4 composition inside behaviors | Kept |
| F5 resolve, then publish | **Kept,** as request → resolve → `PointerCaptureNotification` (§5.2) |
| F6 settings captured at press, F7 channel swap cancels, F9 the interaction owns the value | Kept |
| F8 publishing tolerates tree changes | Kept, as passes, the outbox and snapshot routes (§4.1) |
| F10 `Behavior` base, `SetBehaviors(Spread<Behavior>)` | `SetBehaviors(Spread<IBehavior>)`, no base |
| D1 lazy per-root state | Kept for `NotificationDispatcher` and its parts |
| D2 mapped positions in `PointerEvent` | The sender's matrices. Recognizers freeze them at press |
| D3 push cancels from detach, hide and dispose | Kept, as queued `PointerCancelNotification` with control delivery (§5.4) |
| D4 the provider calls behaviors | Gone. The dispatcher orchestrates delivery; behaviors are called by the route |
| D5 return value, TP touch promotion | Kept |
| D6 `SKMatrix` inside, Stride `Matrix` in public | Kept |
| D7 walk only when needed | Kept. The hit test runs only for the hit route; other routes use stored paths |

---

## 10. Trade-offs

| | v1 (own pipeline) | v2 (notification-native) |
|---|---|---|
| VL content in a node's `Layer` | Gets nothing | Gets the notifications, in node space |
| Patched behaviors | Not possible; only Fu's `Behavior` subclasses | Any `IBehavior`. Claims, cancel and dispatch are open to patches |
| Custom notifications | Stop at the root | Broadcast, or hit path when positional; `Dispatch` for subtrees |
| Cross-node arbitration | Exact | Exact for claims (resolve, then publish). Pending feedback such as `IsPressed` is published immediately and reset by cancel |
| Delivery of a claim start | Same pass | One extra queued pass (`PointerCaptureNotification`) in the same frame |
| Allocation per event | None | One sender, notification and `CallerInfo` per node on the route, as in VL |
| Position decoding | Once, at the root | Once per Fu behavior that looks |
| Shared state | Provider with slots, candidates and hover | The dispatcher: passes, the outbox, claims, recorded paths, hover |
| Public surface | Small | Larger: the sender, six notification types, the claim extensions, `GroupTransaction` |

---

## 11. Code rules added by v2

- **Ambient state only for passes.** `InteractionPass.Current` is the one ambient value. It is pushed and popped in `try`/`finally` by the dispatcher and nothing else.
- **Nothing is delivered during a delivery,** except queries (§4.3).
- **Claims hold owners by reference for the shortest time** (§5.4). No weak references, no finalizers.
- **Control notifications never depend on a return value.**
- Wrong calls (outside a pass, a foreign notification, writes during a query) log a warning and do nothing. They never throw.

---

## 12. Review feedback

| # | Feedback | Verdict | Where |
|---|---|---|---|
| R1 | Capture doesn't preserve gesture coexistence | Valid. Revision 1 had one owner per pointer: same-node behaviors of equal kind failed, and multi-pointer pinch had no rule | Claims with members, all-or-nothing pointer sets, hover outside press state (§5.1) |
| R2 | "Capture, then publish" isn't "resolve, then publish" | Valid. An inner capture published before an ancestor's higher-kind request later in the same pass | Request → resolve after the pass → grant (§5.2) |
| R3 | Control notifications need different delivery guarantees | Valid. A `Layer` returning true, or a display gate, could block a cancel or a leave | Delivery classes: input, control, fan-out (§3.2) |
| R4 | Capture cleanup needs an owner-lifetime contract | Valid. A patch reload or a behavior removed from the spread would leave a dead owner holding the pointer | Implicit release on Up, the behavior-spread diff, node lifetime (§5.4) |
| R5 | Copying the route doesn't fully solve reentrancy | Valid. Nested notifications and mid-pass cancels still interleave | One pass per root, outbox, snapshot routes, queries as the only nested delivery (§4.1, §4.3) |
| R6 | Fu sender metadata doesn't survive every native VL layer | Valid, confirmed in source: `TransformUpstream` replaces the sender, and `CallerInfo` has no slot | The sender carries spaces only; identity comes from the ambient pass (§3.4, §4.2) |
| R7 | The fallback route is too broad for pointer-related input | Valid. VL's `MouseClickNotification`, gesture sequences and positional custom notifications were broadcast | Classification by type; broadcast only for non-pointer notifications (§3.1) |
| R8 | Group notifications need transaction membership | Valid. Selection changes during a drag broke `Begin`/`End` pairing | `GroupTransaction` with members joined at `Begin` (§7.2) |

---

## 13. Decisions

Decided:

| # | Decision |
|---|---|
| Q1 | Occlusion: a pointer over a node doesn't fall through to the siblings behind it. `PointerEvents = None` opts out |
| Q2 | All eligible behaviors on a node receive a notification, even after one returns true |
| Q3 | A claimed pointer's input reaches only the claim node; its ancestors forward only |
| Q4 | Selection bounds through a collecting notification, now a query (§4.3) |
| Q5 | `FuNotificationSender` is public and read-only, spaces only |
| Q6 | The non-root `ILayer.Notify` of a node forwards to the root's dispatcher; only the root chooses routes |
| O1 | One mouse pointer, with chorded buttons (browser semantics) |
| O2 | `IsPressed` is shown immediately. It stays during a drag claimed by the same node, and is cleared when another node takes over. A drag always suppresses `OnClick` |
| O3 | The orchestrator is `NotificationDispatcher` |
| O4 | `Dispatch` is queued; `Query` is synchronous and read-only |
| O5 | A group member that leaves keeps its latest value and stops participating; the remaining members continue |

---

## 14. Phases

| # | Phase | Contents | Depends on |
|---|---|---|---|
| N1 | Plumbing | Rename to `Interactive`; `IInteractive` drops `IBehavior`; `OrderedChildren`; `PointerEvents` style; delete `Gesture.cs`; the `SetBehaviors` pin group | none |
| N2 | Delivery | `Forward` mirroring `Render`; `FuNotificationSender`; delivery classes; classification; broadcast, hit and target routes; hit test (v1 I3) | N1 |
| N3 | Orchestration | `NotificationDispatcher`: passes, `InteractionPass` and the ambient stack, the outbox and its guard, snapshot routes; `Dispatch` and `Query`; `HoverTracker`; touch promotion | N2 |
| N4 | Claims | `PointerClaims`: recorded paths, requests, resolution, `PointerCaptureNotification`, `PointerCancelNotification`, every lifetime rule of §5.4, including the `SetBehaviors` diff | N3 |
| N5 | Recognizers | v1 I6, with `Process` returning the state; `PointerInput.TryRead` | N1 |
| N6 | Headless | `Hoverable`, `Clickable`, `Draggable`, `Pinchable` | N4, N5 |
| N7 | Selection | `Selectable`, `SelectionProvider`, `SelectionNotification`, the bounds query | N6 |
| N8 | Move and transform | `Movable`, `Transformable`, `GroupTransaction`, `GroupManipulationNotification` | N7 |
| N9 | Canvas | `CanvasProvider` | N6 |
| N10 | Docs | | N9 |

Tests for N3 and N4 come first in the suite. They drive passes on a synthetic tree with fake behaviors and check order, delivery classes, claim resolution, the outbox, the lifetime rules, and that no notification is delivered inside another.
