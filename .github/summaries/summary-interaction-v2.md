# Interaction v2: notification-native design

An alternative to [summary-interaction.md](summary-interaction.md) (v1). v1 stops VL notifications at the Fu root and replaces them with a private pipeline: `PointerEvent`, `PointerEventsProvider`, and recognizers called by the provider. v2 keeps VL's notification flow: **a Fu node is a VL layer in `Notify` as it is in `Render`.** Layers handle the notifications they understand, behaviors handle theirs, and Fu adds only what VL lacks: pointer routes, capture, and a few notifications of its own.

What v2 does not change from v1:
- the tier split (recognizers, headless behaviors, scopes, manipulators);
- the §6.1 channel table and cancel writes;
- the §7 math;
- the selection and canvas semantics (C1–C8, S1–S8);
- the code rules in §8, except where noted below.

Sources: VL.Skia `ILayer` / `IBehavior` (`VL.Core.Skia\Interfaces.cs`), `SetBehavior` (`VL.Skia\src\Layers.cs`), `TransformUpstream` and `IntermediateTransformNotificationSender` (`VL.Core.Skia\Transform.cs`), `Group` (`VL.Skia\src\Group.cs`), `SkiaRenderer.OnNotification`.

---

## 1. What VL already does

| VL convention | Where | v2 keeps it as |
|---|---|---|
| A layer notifies upstream first and runs its own logic only if upstream didn't process the notification | `ILayer.Notify` doc, `SetBehavior.Notify` | A node offers each notification to its children, then its `Layer`, then its behaviors |
| `Group` asks the front input first and stops at the first `true` | `Group.Notify` | Children are visited front to back, through the same `OrderedChildren` as `Render` |
| A transform layer pushes its matrix onto `CallerInfo` and wraps the notification with a sender whose `MapFromPixels` maps into the new space | `TransformUpstream.Notify` | Each node wraps with `FuNotificationSender`; `PositionInWorldSpace` is in the node's own space for anything inside it |
| A behavior is anything with `bool Notify(INotification, CallerInfo)` | `IBehavior` | `SetBehaviors(Spread<IBehavior>)`: Fu's C# behaviors and patched behaviors alike |
| `true` means processed; the renderer stores it in `Handled` | `SkiaRenderer.OnNotification` | Same. Touch promotion still relies on it (v1 TP) |

What VL doesn't have, and v2 adds:
- **Occlusion.** `Group` falls through to the next input when the front one returns false. Fu nodes are boxes: a pointer over a node doesn't reach the siblings behind it (G1).
- **Implicit and explicit capture.** In VL, a dragging behavior keeps working only while notifications happen to reach it. In v2, a pressed pointer keeps its route, and a behavior can capture it.
- **Claims between behaviors.** Ancestor drag over child click, pinch over drag (v1 §5.3), expressed through capture and a cancel notification.

---

## 2. Architecture

**A notification takes a route. Nodes forward it along the route the way layers do. Behaviors decide for themselves, and capture is the only shared state.**

| Concern | Owner | Kind |
|---|---|---|
| Forwarding: the transform push, sender wrap, child order, gates | `Interactive<TSelf>.Notify`, mirroring `Renderable.Render` | node base |
| Which route a notification takes | `NotificationRoute`, chosen once at the root | internal enum + path |
| Pressed pointers: their recorded paths and captures | `PointerCapture`, one per root | internal class, no callbacks into behaviors |
| The node and spaces for whoever receives it | `FuNotificationSender : IWorldSpace2d, IProjectionSpace` | public class |
| What happened | recognizers, called by their own behavior | internal classes |
| Where it goes in the patch | behaviors: any `IBehavior`; Fu's are `Hoverable`, `Clickable`, … | VL nodes |
| Fu's own events | `PointerCancelNotification`, `PointerEnterNotification`, `PointerLeaveNotification`, `SelectionNotification`, `GroupManipulationNotification` | public `NotificationBase` subclasses |

```
SkiaRenderer -> root.Notify(notification, caller)              public ILayer.Notify: the only entry
  choose the route (§3): hit path, recorded path, capture path, target, subtree or broadcast
  root.Notify(notification, caller, route)                      internal overload, same body on every node
    if not displayed: return false
    inner  = caller.WithTransformation(caller.Transformation · LocalTransformation)
    wrapped = notification.WithSender(new FuNotificationSender(this, inner, ...))
    1. children on the route, front to back -> child.Notify(notification, inner, route); stop at true
    2. Layer?.Notify(wrapped, inner)                             VL content of this node; stop at true
    3. every behavior: behavior.Notify(wrapped, inner)          all of them; true if any returns true
  root: hover diff -> Enter / Leave notifications (§3.4); Up -> PointerCancel to the rest of the path (§4.3)
  return handled (touch promotion as in v1)
```

v1's `PointerEvent`, `PointerEventsProvider`, `IGestureRecognizer.Publish`, the `Behavior` base class and `IGroupManipulable` are not needed.

---

## 3. Routes

### 3.1 Which route

| Notification | Route | Gate at each node |
|---|---|---|
| Pointer Down, Wheel, a Move with no button held (mouse, touch, VL `GestureNotification`) | **Hit path:** the front-most displayed node under the point and its ancestors (v1 §5.2) | Bounds, `ClipArea`, display, `PointerEvents` |
| Move or Up of a pressed pointer, no capture | **Recorded path:** the hit path from its Down | None; the pointer may be anywhere |
| Any event of a captured pointer | **Capture path:** root down to the capturing node | None |
| `PointerCancelNotification` | The pointer's recorded path | None |
| `PointerEnterNotification`, `PointerLeaveNotification` | **Target:** one node; its ancestors only forward | None |
| `SelectionNotification`, `GroupManipulationNotification`, and anything sent with `Dispatch` | **Subtree** of the dispatching node | Display |
| Everything else: keys, focus, VL's `MouseClickNotification`, notifications from patches | **Broadcast,** VL `Group` semantics: every child, front to back, until one returns true | Display |

- Pointer notifications that reach a node also reach its `Layer`. VL content in a node therefore gets the same events, in node space, that it would get under a `TransformUpstream`.
- Nodes with `PointerEvents = None` are skipped only by the hit path. Broadcasts and subtree notifications still reach them.

### 3.2 Order and "processed"

- Each node forwards inward first: route children, then `Layer`, then behaviors. The deepest node acts first and ancestors after it, like a bubbling event.
- A `true` from a child or from the `Layer` stops the node's own layer and behaviors (VL semantics). A patch that processes a notification in a node's layer keeps it from Fu's behaviors, which is what the patch asked for.
- **All behaviors on one node see the notification,** even after one returns `true`. They belong to the same node, as `Clickable` and `Selectable` do in v1 ("equal kinds on the same node proceed").
- Fu behaviors return `true` only when they finish or hold something exclusively: a click fired on Up, a captured drag, a handled wheel. While a gesture is only pending they return false, so the Down still reaches ancestors (an ancestor drag can start).

### 3.3 Sender and spaces

`FuNotificationSender` is created per node per notification, like VL's `IntermediateTransformNotificationSender`.

| Member | Meaning |
|---|---|
| `Node` | The `IInteractive` that forwarded the notification |
| `WindowToNode`, `WindowToParent` | The current inverses for that node, as `SKMatrix` internally; public as Stride `Matrix` (D6) |
| `Scaling`, `TimeMs` | From the root: `CallerInfo.Scaling` and the root's stopwatch |
| `MapFromPixels` (`IWorldSpace2d`) | Window position to node space, so `PositionInWorldSpace` works as in any VL layer |
| `MapFromPixels` (`IProjectionSpace`) | Forwarded from the original notification, as VL does |

Recognizers freeze `WindowToParent` at press (A1), so a drag offset stays correct while the node moves.

**Allocation:** wrapping allocates one sender and one notification per node on the route, and `CallerInfo.WithTransformation` allocates as well. VL's transform layers do the same, and routes are O(depth). The v1 rule "no allocation per event" is relaxed to "no allocation inside recognizers and behaviors".

### 3.4 Hover

- The root keeps the last hover path: the hit path of the mouse or primary touch, or the capture path while captured (G3).
- After each pointer notification it compares the new path with the last one. Nodes that left get a `PointerLeaveNotification` and nodes that entered get a `PointerEnterNotification`, routed to that node only (target route).
- `Hoverable` writes `IsHovered` from those. A patched behavior can use them the same way.

---

## 4. Capture and claims

### 4.1 `PointerCapture`

One per root, created lazily (v1 D1). It holds, per pointer id (the mouse is −1; touches use their `Id`):
- the path recorded at Down, as a reused node buffer;
- the capture, if any: owner (the behavior, as `object`), owner node, and `GestureKind`.

```csharp
public enum GestureKind { Click = 1, Drag = 2, Pinch = 3 }

// On the node, so C# and patched behaviors use the same call.
public bool TryCapturePointer(INotification notification, object owner, GestureKind kind);
public void ReleasePointer(INotification notification, object owner);
```

The pointer id and the node come from the notification and its `FuNotificationSender`. A notification that didn't come through a Fu node returns false with a warning.

### 4.2 Claim rules as capture rules

| Situation | Result |
|---|---|
| No capture yet | Succeeds |
| Same owner | Succeeds (no change) |
| Another owner, **higher** kind | Succeeds and takes over (v1: a higher kind cancels lower ones, and pinch steals from drag) |
| Another owner, equal or lower kind | Fails. The behavior must not publish a start (v1 F5 becomes **capture, then publish**) |

- Front-most wins naturally: inner nodes see the event first, so for equal kinds on the same event the inner node captures first.
- Clicks don't capture. A click stays pending until Up, and the claim rules reach it through cancel.

### 4.3 `PointerCancelNotification`

`PointerCancelNotification(pointerId, owner)` goes along the pointer's recorded path. Every behavior that took part in that pointer resets, unless it is `owner`. Behaviors that published something write their idle facts (v1 B7); behaviors that never published stay silent.

The root sends it when:

| Source | `owner` |
|---|---|
| A capture succeeds | the new owner; everyone else on the path cancels |
| Up was handled | the behavior that processed the Up, or null. This reaches ancestors whose Up never arrived because an inner node returned true, e.g. an ancestor `Clickable` still showing `IsPressed` |
| Esc, `MouseLostNotification`, `LostFocusNotification` | null: everyone cancels |
| Node detached, hidden, disposed, or `PointerEvents` set to None (v1 §5.7) | null, along the paths that contain the node, before it leaves |
| `SetEnabled(false)`, a channel swap | not sent: the behavior cancels itself (v1 F6, F7) |

Patched behaviors receive it too, and can reset on it.

### 4.4 Reentrancy

Channel subscribers run synchronously and may change the tree. Routes iterate over a copy of the path, and each node checks that it is still attached to the same root before forwarding (v1 F8).

---

## 5. Behaviors

### 5.1 Contract

- Behaviors are plain `IBehavior`. There is no Fu base class; `SetBehaviors(Spread<IBehavior>)` (pin order 25) accepts Fu's behaviors and patched ones alike.
- A Fu behavior:
  - decodes the notification with `PointerInput.TryRead(notification, out PointerSample sample)`. The sample holds the kind, pointer id, buttons, modifiers, window position, the sender's matrices, `Scaling` and `TimeMs`. It is decoded once per behavior, by one shared switch;
  - feeds its own recognizers and reads their state synchronously;
  - captures before it publishes a start;
  - writes channels.
- Recognizers keep v1's state machines (enum + `switch`, idle field initializers, `Cancel()` returns to them). `Process` returns the new state directly. Nothing needs deferring now, so recognizers don't use `IObservable`; public `IObservable<TState>` outputs remain a later option.
- `SetEnabled` is on each Fu behavior. A small internal helper holds the shared compare-and-cancel code; there's no base class.
- One instance per node is a convention, as for VL behaviors on layers. During a gesture, a Fu behavior ignores notifications whose `Node` isn't the node it pressed on.

```csharp
public sealed class Draggable : IBehavior
{
    private readonly DragGestureRecognizer _drag = new();
    private IChannel<Vector2>? _offset;
    private IChannel<bool>? _isDragging;
    private IChannel<Unit>? _onDragStart;
    private IChannel<Unit>? _onDragEnd;
    private bool _enabled = true;

    public bool Notify(INotification notification, CallerInfo caller)
    {
        if (!_enabled)
            return false;

        if (notification is PointerCancelNotification cancel)
            return OnCancel(cancel);

        if (!PointerInput.TryRead(notification, out var sample))
            return false;

        var drag = _drag.Process(in sample);
        switch (drag.Phase)
        {
            case DragPhase.Started:
                if (!notification.TryCapturePointer(this, GestureKind.Drag))
                {
                    _drag.Cancel();   // lost the claim before publishing anything
                    return false;
                }
                _offset?.EnsureValue(Vector2.Zero);
                _isDragging?.EnsureValue(true);
                _onDragStart.Fire();
                return true;
            case DragPhase.Moved:
                _offset?.EnsureValue(drag.OffsetInParent);
                return true;
            case DragPhase.Ended:
                _offset?.EnsureValue(drag.OffsetInParent);
                _isDragging?.EnsureValue(false);
                _onDragEnd.Fire();
                notification.ReleasePointer(this);
                return true;
            default:
                return false;   // Idle or Pending: let the event reach ancestors
        }
    }

    // Writes the idle facts if a drag had started (B7), then resets the recognizer. Returns false.
    private bool OnCancel(PointerCancelNotification cancel) { /* ... */ }
}
```

`TryCapturePointer`, `ReleasePointer` and `Fire` are extensions over `INotification` and `IChannel<Unit>`. Matching VL nodes give patched behaviors the same calls.

### 5.2 Tier 1

Same channels, settings and cancel writes as v1 §6.1, without `OnDoubleClick`. Differences:
- `Hoverable` reacts to `PointerEnterNotification` and `PointerLeaveNotification`.
- `Clickable` returns true on the Up that fires `OnClick`. The `PointerCancel` that follows the Up resets an ancestor `Clickable` that was also pressed.
- Wheel: the hit path bubbles from the deepest node, and the first behavior that returns true for the wheel consumes it. That is v1's "front-most node with a wheel behavior", with no special case.

### 5.3 Selection and group manipulation as notifications

The providers talk to their items through subtree notifications instead of walks and interfaces. Patched items can take part too.

| v1 | v2 |
|---|---|
| `Selectable` finds its provider by walking up (S1) | Same: walk up from `sender.Node` and find the nearest node with a `SelectionProvider` behavior |
| Provider walks its subtree to clear or select (S3) | Provider dispatches `SelectionNotification(provider, action, target, rect, modifiers)` into its subtree. `action` is `Clear`, `Replace`, `Add`, `Toggle` or `Marquee`. Each `Selectable` acts only if its nearest provider is `provider`, which also hides nested providers' items (S3) |
| Provider computes the bounds by walking selected items (S8) | Provider dispatches `SelectionNotification(action = CollectBounds)`. Selected items add their box, mapped into provider space through the sender matrices, to the notification's accumulator |
| `IGroupManipulable.Begin/Apply/End/Cancel` | `GroupManipulationNotification(provider, phase, deltaInProvider)` with `phase` `Begin`, `Apply`, `End` or `Cancel`, dispatched into the subtree. Selected `Movable` and `Transformable` items apply it with v1 §7.2 |

`Dispatch(INotification)` on a node sends a notification into that node's subtree. It uses the caller cached from the node's last `Render` or `Notify`, so behaviors can dispatch from inside their own `Notify`.

### 5.4 Canvas

`CanvasProvider` is unchanged from v1 §6.3 and S4: the right button pans, the wheel and a pinch zoom, the content keeps its own `TransformOrigin`, and the provider compensates with v1 §7.4. The wheel arrives through the normal hit path, so items inside the canvas that consume the wheel (a scroll view) win, because they are deeper.

---

## 6. v1 decisions in v2

| v1 | v2 |
|---|---|
| F1 typed states, `Gesture.cs` deleted | Kept; states are returned by `Process` instead of pushed |
| F2 `PointerEvent` decoded once at the root | Replaced by `PointerInput.TryRead`, once per Fu behavior; VL notifications travel unchanged |
| F3 mutable recognizers | Kept |
| F4 composition inside behaviors | Kept |
| F5 resolve, then publish | **Capture, then publish**, plus `PointerCancel` for losers |
| F6 settings captured at press, F7 channel swap cancels, F9 the interaction owns the value | Kept |
| F8 publishing tolerates tree changes | Kept as route iteration over a copied path (§4.4) |
| F10 `Behavior` base, `SetBehaviors(Spread<Behavior>)` | `SetBehaviors(Spread<IBehavior>)`, no base |
| D1 lazy per-root state | Kept for `PointerCapture` |
| D2 mapped positions in `PointerEvent` | The sender's matrices; recognizers freeze them at press |
| D3 push cancels from detach, hide and dispose | Kept, as `PointerCancelNotification` |
| D4 the provider calls behaviors | Gone: behaviors are called by the route |
| D5 return value, TP touch promotion | Kept |
| D6 `SKMatrix` inside, Stride `Matrix` in public | Kept |
| D7 walk only when needed | Kept: the hit test runs only for the hit route; other routes use stored paths |

---

## 7. Trade-offs

| | v1 (own pipeline) | v2 (notification-native) |
|---|---|---|
| VL content in a node's `Layer` | Gets nothing | Gets the notifications, in node space |
| Patched behaviors | Not possible; only Fu's `Behavior` subclasses | Any `IBehavior`; capture and cancel are open to patches |
| Custom notifications (selection, group, user-defined) | Interfaces and walks | Notifications through `Dispatch` |
| Cross-node arbitration | Exact: everyone processes, then one resolve, then publish | Approximate. A front node may publish pending feedback (`IsPressed`) before an ancestor claims; it is corrected by `PointerCancel` within the same notification |
| Allocation per event | None | One sender, notification and `CallerInfo` per node on the route, as in VL |
| Position decoding | Once, at the root | Once per Fu behavior that looks |
| Shared state | Provider with slots, candidates and hover | `PointerCapture` with paths and captures, plus the hover path |
| Public surface | Small | Larger: the sender, five notification types, capture extensions |

---

## 8. Open questions

| # | Question | Default |
|---|---|---|
| Q1 | Occlusion: should a pointer over a node that processes nothing fall through to the sibling behind it, as in VL `Group`? | No: Fu nodes occlude like CSS boxes (G1). `PointerEvents = None` opts out. |
| Q2 | Should all behaviors on one node see a notification after one returns true? | Yes (§3.2). |
| Q3 | Should a captured pointer's events also bubble to the capturer's ancestors? | No: the capturer returns true, so VL semantics already stop them. |
| Q4 | Selection bounds through a collecting notification, or a direct subtree walk in C#? | The notification, so patched items can report bounds. |
| Q5 | Should `FuNotificationSender` be public, so patches can read `Node` and the matrices? | Yes, read-only. |
| Q6 | Should the non-root `ILayer.Notify` of a node be callable from outside, e.g. when a node is used as a plain layer? | No: it forwards to the root entry, and only the root chooses routes. |

---

## 9. Phases (if v2 is chosen)

| # | Phase | Contents | Depends on |
|---|---|---|---|
| N1 | Plumbing | Rename to `Interactive`; `OrderedChildren`; `PointerEvents` style; delete `Gesture.cs`; `SetBehaviors(Spread<IBehavior>)` | none |
| N2 | Routing | Internal `Notify` overload mirroring `Render`; `FuNotificationSender`; broadcast and hit routes; forwarding to `Layer`; hit test (v1 I3) | N1 |
| N3 | Capture | `PointerCapture`: recorded paths, capture rules, `PointerCancelNotification`, cancel sources, the hover path with Enter and Leave; `Dispatch`; touch promotion | N2 |
| N4 | Recognizers | v1 I6, with `Process` returning the state; `PointerInput.TryRead` | N1 |
| N5 | Headless | `Hoverable`, `Clickable`, `Draggable`, `Pinchable` | N3, N4 |
| N6 | Selection | `Selectable`, `SelectionProvider`, `SelectionNotification` | N5 |
| N7 | Move and transform | `Movable`, `Transformable`, `GroupManipulationNotification` | N6 |
| N8 | Canvas | `CanvasProvider` | N5 |
| N9 | Docs | | N8 |
