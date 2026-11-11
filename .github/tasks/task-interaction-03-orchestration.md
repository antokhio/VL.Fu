# Task interaction-03: Orchestration

Source: [summary-interaction-v2.md](../summaries/summary-interaction-v2.md) §2.2, §3.1, §3.4, §3.5, §4, §11, §14 N3; v1 TP (touch promotion). Depends on: [task-interaction-02](task-interaction-02-delivery.md).

## Goal

Every notification a root receives runs as a **pass**, one at a time per root. After this task:
- `NotificationDispatcher`, one per root and created lazily, runs passes and an outbox. Nothing is delivered in the middle of another delivery;
- `InteractionPass` is the ambient context of the running pass. Calls from anywhere inside a node, including below a native transform layer in its `Layer`, find the node through it;
- `Dispatch` queues a notification into the current node's subtree; `Query` delivers one there immediately and read-only;
- `HoverTracker` turns the hover path of the mouse or primary touch into `PointerEnterNotification` and `PointerLeaveNotification`;
- a touch that reaches the tree is reported as processed, so Windows doesn't promote it to mouse input, and mouse input right after a touch is ignored;
- `FuNotificationSender` gets `TimeMs`.

Claims, recorded paths and the lifetime rules of §5.4 are N4.

## Design

### 1. Types

| Type | Kind | Role |
|---|---|---|
| `NotificationDispatcher` | internal sealed class | One per root. `Run`, the outbox and its guard, the pass pool, routes, bookkeeping after a pass, `Dispatch` and `Query` |
| `InteractionPass` | internal sealed class, pooled | What a running pass knows: root, notification, caller, class, route, `TimeMs`, the node whose receivers run, whether it is a query. Ambient through `Current` |
| `Delivery` | internal readonly record struct | `(DeliveryClass Class, RouteKind Route, IInteractive? Node)`: a route plan. The route itself is built when the pass starts |
| `RouteKind` | internal enum | `Broadcast`, `Hit`, `Target`, `Subtree` |
| `NotificationRoute` | internal readonly struct | The path and two flags, over the pass's pooled path list |
| `INotificationRoot` | internal interface | What the dispatcher needs from a root: `Forward(pass)` and `TryBuildHitPath`. `Renderable<TSelf>` implements it explicitly |
| `HoverTracker` | internal sealed class | Keeps the hover path; diffs it into the nodes left and entered |
| `TouchPromotionGuard` | internal sealed class | Touches that are down, the time the last one lifted, and whether a mouse notification is a promoted one |
| `PointerIds` | internal static class | `Mouse = -1`; the pointer id of a notification (mouse, touch, gesture) |
| `InteractionLog` | internal static class | Warnings from calls that may have no node: the node when known, otherwise the app host's logger |
| `PointerEnterNotification`, `PointerLeaveNotification` | public sealed, `NotificationBase` | `PointerId`. Control, target route |
| `NotificationExtensions` | public static class | `Dispatch`, `Query`: VL nodes through `ImportAsIs` |

### 2. `Run` (v2 §2.2, §4.1)

```
root.Notify(notification, caller)                 a child forwards to its root (Q6)
  dispatcher.Run(notification, caller, Classify(notification))

Run(notification, caller, delivery)
  a pass is running on this root: enqueue (notification, caller, delivery), return false     rule 1
  running = true
  handled = RunPass(...)
  drain the outbox, first in, first out, each entry as its own pass                         rule 3
    more than 64 passes: warn on the root, drop the rest
  running = false; the outbox is cleared (it is only non-empty after an exception or the guard)
  return handled

RunPass(notification, caller, delivery)
  a promoted mouse notification (§6): return false, nothing else happens
  rent a pass; Begin: it becomes InteractionPass.Current                                     try/finally
  build the route into the pass's path list (§3); none: skip the delivery
  handled = root.Forward(pass)
  bookkeeping: touch tracking (§6), hover diff (§5), whose enter and leave go to the outbox
  touch promotion: a TouchNotification whose route was built is handled
  End: the previous pass becomes Current again; return the pass to the pool
```

- **The delivery is planned when the notification is queued,** but its route is built when its pass starts (rule 4). A queued notification sees the tree as it is then.
- Re-entrant input (a behavior calling `Notify` on the root) is queued like Fu's own notifications, and returns false.
- If a receiver throws, the pass still ends and `Current` is restored. The exception propagates to the renderer, which reports it; the rest of the outbox is dropped.

### 3. Routes

| `RouteKind` | Path | Bubbles | Descends | Used by |
|---|---|---|---|---|
| `Broadcast` | `[root]` | (n/a) | yes | keys, custom notifications, device loss |
| `Hit` | the hit path at the notification's position | yes | no | positional input |
| `Target` | root … node | no | no | enter, leave |
| `Subtree` | root … node | no | yes | `Dispatch`, `Query` |

- `Target` and `Subtree` need their node under this root when the pass starts. Otherwise nothing is delivered.
- The path list belongs to the pass and is reused, so building a route doesn't allocate once the pool is warm.

### 4. Forwarding changes

`Forward(notification, caller, pass, depth)` replaces `Forward(..., delivery, depth)`:
- **Snapshot check (rule 4).** A node checks that it is still attached to the pass's root when it is reached, and again before its own receivers run. A node detached during the pass is skipped, together with its subtree. Descending also checks that each child still belongs to the node.
- **Pass node.** The node sets `pass.Node` to itself while its `Layer` and behaviors run, and restores the previous value afterwards.
- **Sender.** `FuNotificationSender` gets `pass.TimeMs`.

The internal `Deliver` is removed: tests deliver along explicit routes through `Dispatcher.Run(notification, caller, delivery)`.

### 5. Hover (v2 §3.5)

- **Updates the hover path:**
  - every input mouse notification except `MouseLost`: the pass's hit path, empty when nothing was hit;
  - a `TouchNotification` of the primary touch: its hit path on Down and Move, empty on Up, since a lifted finger hovers nothing.
- **Clears the hover path:** `MouseLostNotification`.
- The diff keeps the common prefix of the old and new path. The nodes left get `PointerLeaveNotification`, the deepest first; the nodes entered get `PointerEnterNotification`, the outermost first (as in the DOM). Both are queued as control deliveries on a target route.
- `PointerId` is −1 for the mouse and the touch id for a touch. `ModifierKeys` and `Sender` come from the notification that moved the pointer.
- A hidden node on the old path gets its leave, since control delivery reaches it. A node already detached from the root doesn't: the leave sent before a node leaves is N4's lifetime hook.

### 6. Touch promotion (v1 TP)

- Windows turns a touch that the window didn't handle into mouse input. `SkiaRenderer` stores `Notify`'s result in `Handled`, so **a `TouchNotification` whose route was built returns true**, even if no receiver processed it. A touch outside every node returns false, and VL layers behind the tree still get it.
- **Backstop.** While a touch is down, and for 100 ms after the last one lifted, mouse notifications are ignored: not delivered, no hover update, `false`. `MouseLostNotification` is never ignored.
- `MouseLostNotification` and `LostFocusNotification` forget the touches that are down, so a lost Up can't block the mouse.
- Time comes from the dispatcher's clock: `TimeProvider.System` by default, set by tests with the internal `SetClock`.

### 7. `Dispatch` and `Query` (v2 §4.2, §4.3)

```csharp
public static class NotificationExtensions
{
    public static void Dispatch(this INotification notification, INotification toDispatch);
    public static void Query(this INotification notification, INotification query);
}
```

- Both act from the current pass: `InteractionPass.Current`, at `pass.Node`.
- **`Dispatch`** queues `toDispatch` as a fan-out on the subtree route of `pass.Node`. The node itself receives it too. It runs after the current pass, with the current pass's caller.
- **`Query`** delivers `query` immediately as a nested pass: fan-out, subtree route of `pass.Node`, marked as a query. It returns when every node has seen it. Handlers only add to the query's own accumulator.
- **Wrong calls log a warning and do nothing:**
  - outside a pass (logged on the sender's node when the notification has a `FuNotificationSender`, otherwise on the app host);
  - with a notification the current pass isn't delivering;
  - during a query (`Dispatch` and nested `Query` alike);
  - with an empty notification to send.
- **"The notification the pass is delivering"** is recognized by type and payload (position, client area, modifier keys). A native layer inside the `Layer` re-wraps it into a new instance, so a reference check would reject legitimate calls.

### 8. `InteractionPass`

- `Current` is thread-static. `Begin` remembers the pass that was current and `End` restores it, so a Fu root inside another root's `Layer` nests its pass. Only the dispatcher calls them (§11).
- Pooled per dispatcher: a query nests a second pass, so the pool holds two in practice.
- `TimeMs`: milliseconds since the dispatcher was created, taken when the pass begins. A query uses its outer pass's time.

### 9. Lifetime of the dispatcher

- Created by the root's first `Notify`. A root that gets a parent drops it, and so does `Dispose` (§5.4 row 8): hover and the touch guard are forgotten without notifications.

## Decisions (settled)

1. **Hover path updates** come from every mouse input notification and the primary touch. A primary touch lifting leaves everything.
2. **Enter and leave carry only the pointer id.** Their positions are in the Move that caused them.
3. **The dispatching node receives its own `Dispatch`.** A provider ignores its own notifications by comparing the sender.
4. **Nested queries are rejected,** like any other call during a query.
5. **Pass ownership is checked by type and payload,** not by reference (§7).
6. **Promoted mouse input returns false,** so it isn't reported as processed when the touch wasn't.

## Tests

`VL.Fu.Tests\Core\Interaction\DispatcherTests.cs` (new), on a root with two siblings and a child, with behaviors that log and can run an action per notification:

- **Passes.** `Current` is null outside a pass, and its `Node` is the receiving node inside. A notification sent from a behavior is queued, runs after the pass, and the inner `Notify` returns false. A Fu root inside a node's `Layer` nests its pass and restores the outer one. A throwing behavior ends the pass and the next notification is delivered.
- **Snapshot.** A node detached by an earlier sibling's behavior is skipped. A node detached by its child's behavior doesn't run its own receivers, and its ancestors still do.
- **Outbox guard.** A behavior that dispatches on every notification it receives stops after 64 passes with a warning, without overflowing the stack.
- **`Dispatch`.** Queued after the pass, into the node's subtree including the node; hidden nodes skipped. Outside a pass, with a foreign notification, or during a query: nothing.
- **`Query`.** Delivered before `Query` returns, into the subtree only. `Dispatch` from a query handler is rejected.
- **Hover.** Enter outermost first, leave deepest first, only the target receives. Moving off the tree leaves everything. `MouseLost` leaves everything. A node hidden while hovered gets its leave. `PointerEvents = None` isn't hovered. A primary touch hovers until it lifts.
- **Touch promotion.** A touch on the tree returns true; outside, false. Mouse input during a touch and 50 ms after it is ignored; after 100 ms it is delivered. `MouseLost` is never ignored.
- **Sender.** `TimeMs` follows the dispatcher's clock.

`DeliveryTests` keep their expectations: their recorders ignore enter and leave, and target and fan-out deliveries go through `Dispatcher.Run`.

## Deferred

* Recorded paths, claim routes, the hover path while claimed (G3), `RequestPointerCapture` / `ReleasePointerCapture`, cancels: N4.
* The lifetime hook (detach, hide, dispose, `PointerEvents = None`) that queues cancels and leaves before a node leaves: N4, shared by claims, recorded paths and hover.
* `PointerInput.TryRead` reading `TimeMs`: N5.

## Risk

* Mouse input is ignored for 100 ms after a touch lifts. A mouse used right after touching the screen loses its first event.
* A touch whose Up never arrives blocks the mouse until the next device loss.
