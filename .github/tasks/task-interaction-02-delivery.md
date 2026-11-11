# Task interaction-02: Delivery

Source: [summary-interaction-v2.md](../summaries/summary-interaction-v2.md) §1, §2.2, §3.1–§3.4, §14 N2; v1 I3 (hit walk). Depends on: [task-interaction-01](task-interaction-01-plumbing.md).

## Goal

A Fu tree takes part in VL's notification flow the way it takes part in rendering. After this task:
- `Notify` on a root classifies the notification, chooses its route, and forwards it down the tree;
- every node forwards like a VL layer: route children front to back, then its `Layer`, then all its behaviors;
- a positional notification takes the hit path. The front-most node under the pointer wins, and nothing falls through to the siblings behind it;
- the input, control and fan-out delivery classes behave as in v2 §3.2;
- `Layer` content and behaviors get a `FuNotificationSender`, so `PositionInWorldSpace` is in the node's own space.

There is no pass, outbox, press tracking or claim yet. Each `Notify` is delivered directly (N3 wraps it in a pass), and every pointer notification takes a fresh hit path (N4 adds recorded paths and claim routes).

## Design

### 1. Placement (decided)

| Member | Class | Why |
|---|---|---|
| `Notify`, `Forward`, `Deliver` | `Renderable<TSelf>` | They call `Layer.Notify`, and `Layer` lives there. `Notify` sits next to `Render` and mirrors it |
| `TryBuildHitPath` | `Interactive<TSelf>` | It needs only layout, clip, `PointerEvents` and `OrderedChildren` |
| `NotificationRoute`, `DeliveryClass`, `Delivery`, `NotificationClassifier` | `Core\Interaction` | Internal |
| `FuNotificationSender` | `Core\Interaction` | Public, read-only |

The `Notify` stub on `Interactive` is removed. `IRenderable : ILayer` binds to the one on `Renderable`.

### 2. Classification (v2 §3.1, the N2 subset)

`NotificationClassifier.Classify(INotification)` returns a `DeliveryClass` and a `RouteKind`:

| Notification | Class | Route |
|---|---|---|
| `MouseLostNotification`, `LostFocusNotification`, `KeyboardLostNotification` | control | broadcast |
| any other `INotificationWithPosition`: mouse, touch, gesture, click, custom | input | hit |
| anything else: keys, focus, custom | input | broadcast |

`MouseLostNotification` has a position, so it is matched before the positional row. N4 adds the recorded path for pressed pointers, the click's Up path and gesture sequences. N3 and N4 add the rows for Fu's own notifications.

### 3. Routes

`NotificationRoute` is an internal sealed class: a path from the root down, and two flags.

| Factory | `Path` | Nodes above the last one | The last node |
|---|---|---|---|
| `Broadcast(root)` | `[root]` | (none) | receives, and descends into its whole subtree |
| `Hit(path)` | the hit path | receive (bubbling) | receives, doesn't descend |
| `Target(node)` | root … node | only forward | receives, doesn't descend |

- `Receives(depth)`: the last node, everything below it, and the nodes above it when the route bubbles.
- `Descends(depth)`: at or below the last node, when the route descends.
- N3 adds `Subtree(node)` (forward only, then descend) for `Dispatch`; N4 adds the claim route (forward only, then the claim node).

### 4. Hit test (v1 I3)

`internal bool TryBuildHitPath(Vector2 position, SKMatrix callerTransformation, List<IInteractive> path)` on `Interactive<TSelf>`. `position` is the notification's `Position`, in the same pixel space as `CallerInfo.Transformation`.

At each node:
1. Skip the node and its subtree when it wasn't laid out, its display is none, or `PointerEvents` is `None`.
2. `nodeToWindow = parentToWindow · LocalTransformation`, the same matrix `Render` uses. If it can't be inverted (scaled to zero), skip.
3. Map the position into node space and push the node onto `path`.
4. Children are tested back to front over `OrderedChildren`, but only if the point is inside `ClipArea` when the node clips. The first child that is hit ends the walk (occlusion, Q1).
5. Otherwise the node itself is hit when the point is inside `[0, width) × [0, height)`. The right and bottom edges are excluded so neighbors don't share a pixel.
6. If neither, pop the node and report a miss.

An ancestor whose box doesn't contain the point is still on the path when a visible-overflow child is hit, as in the DOM. A point outside every node gives an empty path, and the notification isn't delivered: `Notify` returns false.

The path list is allocated per notification. N3 pools it per root.

### 5. Forwarding (v2 §2.2, §3.2, §3.3)

`internal bool Forward(INotification notification, CallerInfo caller, Delivery delivery, int depth)` on `Renderable<TSelf>`. `depth` is the node's index on the route's path, or larger below its end.

1. **Gate.** Input and fan-out skip a node that isn't laid out or whose display is none, together with its subtree. Control has no gate.
2. `inner = caller.WithTransformation(caller.Transformation · LocalTransformation)`.
3. **Children.**
   - On the path: only the next path node, and only while it is still a child of this node.
   - Where the route descends: every child of `OrderedChildren`, front to back. Input stops at the first child that returns true (VL `Group`); control and fan-out visit all.
   - Input: a child that returns true ends this node too, with true.
4. **Own receivers,** only where the route says this node receives:
   - `Layer?.Notify(wrapped, inner)`. For input, true ends the node with true, so its behaviors don't see it.
   - Every behavior in `Behaviors`, even after one returns true. Input returns true if any did.
   - `wrapped` is `notification.WithSender(new FuNotificationSender(...))`. It is created only when the node has a `Layer` or behaviors.
5. Control and fan-out return false.

Children always receive the unwrapped notification, so each sender is built from the notification the root received.

`Deliver(notification, caller, delivery)` starts `Forward` at the route's first node with depth 0. It is internal, so N3's dispatcher and the tests can deliver along any route and class.

### 6. `Notify`

```
public bool Notify(INotification notification, CallerInfo caller)
  child: return Root.Notify(notification, caller)          Q6: only the root chooses routes
  (class, kind) = Classify(notification)
  hit:       build the hit path at the position; empty: return false; route = Hit(path)
  broadcast: route = Broadcast(this)
  return Deliver(notification, caller, new Delivery(class, route))
```

`Notify` doesn't capture the viewport; `Render` does.

### 7. `FuNotificationSender : IWorldSpace2d, IProjectionSpace`

Public, sealed, read-only. One instance is created per node per notification, like VL's `IntermediateTransformNotificationSender`. It carries spaces, not identity (v2 §3.4).

| Member | Meaning |
|---|---|
| `IInteractive Node` | The node that forwarded the notification |
| `Optional<Matrix> WindowToNode` | Maps window pixels into the node's space. No value when the node is scaled to zero |
| `Optional<Matrix> WindowToParent` | The same into the parent's space, or the caller's for a root |
| `float Scaling` | `CallerInfo.Scaling` |
| `MapFromPixels` (`IWorldSpace2d`) | The notification's position in node space, so `PositionInWorldSpace` works as in any VL layer. NaN when the node is scaled to zero |
| `MapFromPixels` (`IProjectionSpace`) | Taken from the notification the root received, as VL does |

- Internally the matrices are `SKMatrix?`. Recognizers (N5) read them and freeze `WindowToParent` at press.
- `TimeMs` comes with the root's stopwatch in N3.

## Decisions (settled)

1. **`Notify` and `Forward` live on `Renderable`;** the hit test and routes on `Interactive` (user, N2 cut).
2. **No fall-through.** A point outside every node delivers nothing. The root fills the viewport by default, so this only matters for a root with smaller explicit bounds.
3. **`PointerEvents = None` skips the subtree** (N1). The children can't opt back in.
4. **Only input bubbles to ancestors' own receivers.** Target routes forward through the ancestors.
5. **Public matrices are `Optional<Matrix>`,** following the facade rule for values that can be undefined.

## Tests

`VL.Fu.Tests\Core\Interaction\DeliveryTests.cs` (new). A 100 × 100 root with children of fixed sizes, recording layers and behaviors that log their name and `PositionInWorldSpace`:

- **Hit path.**
  - A press on a child reaches the child's layer, then its behaviors, then the root's layer and behaviors, in that order.
  - Front-most of two overlapping siblings wins, and the one behind gets nothing even when the front returns false.
  - A point outside every node delivers nothing and returns false.
  - The right and bottom edges miss.
  - A clipped child isn't hit outside its parent's clip area. A visible-overflow child is hit outside its parent, and the parent still bubbles.
  - `PointerEvents = None` passes through to the node behind. Display none and not laid out aren't hit.
  - A rotated and scaled node is hit in its own space.
- **Input semantics.** A child returning true stops the ancestors and returns true. `Layer` true skips the node's behaviors. Every behavior on a node runs after one returns true.
- **Broadcast.** Keys go front to back and stop at the first child that returns true. Hidden nodes are skipped.
- **Control.** `MouseLostNotification` reaches every node, including hidden and `PointerEvents = None` ones, ignores true, and returns false.
- **Target and fan-out** through `Deliver`. A target's ancestors don't receive. Fan-out ignores true and skips hidden nodes.
- **Sender.** `Node`, `PositionInWorldSpace` in node space, `WindowToNode`, `WindowToParent`, `Scaling`, and the projection positions of the original.
- **Classification.** Custom positional notifications take the hit path; custom non-positional ones are broadcast.
- **Q6.** `Notify` on a child delivers from the root.

## Deferred

* Passes, the outbox, `InteractionPass`, pooled paths, `TimeMs`, `Dispatch` and the subtree route, hover: N3.
* Recorded paths, the click's Up path, gesture sequences, claims and cancels: N4.
* Disabled Fu behaviors as "not eligible": N6.

## Risk

* Patches that put content into a node's `Layer` start receiving notifications. Until now `Notify` returned false and they got none.
