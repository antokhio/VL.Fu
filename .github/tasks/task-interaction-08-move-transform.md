# Task interaction-08: Move and transform

Source: [summary-interaction-v2.md](../summaries/summary-interaction-v2.md) §3.1, §5.2, §5.4, §7.2, §14 N8; v1 [summary-interaction.md](../summaries/summary-interaction.md) §6.3, §7.1–§7.3, S6, M1, T3; [review-interaction-architecture.md](../proposals/review-interaction-architecture.md) "Canvas interaction", C7. Depends on: [task-interaction-07](task-interaction-07-selection.md).

## Goal

Items can be moved and transformed with the pointer, alone or as a selected group. After this task:
- `Movable` and `Transformable` are VL process nodes with the N6 shape. Each writes one channel the patch owns and applies to the node;
- dragging a selected item, or dragging inside a provider's `SelectionBounds`, moves every selected item of that provider;
- pinching a selected `Transformable` transforms the group about the pinch center (S6);
- a group operation is a `GroupTransaction` with fixed members, so selection changes in the middle of a drag can't break `Begin`/`End` pairing (R8);
- cancelling restores what the gesture changed, alone or in a group.

## Design

### 1. Types and files

| Type | Kind | File |
|---|---|---|
| `Movable` | public sealed class, `NodeBase, IBehavior` | `Behaviors\Movable.cs` |
| `Transformable` | same | `Behaviors\Transformable.cs` |
| `GroupTransaction` | public sealed class | `Behaviors\GroupTransaction.cs` |
| `GroupManipulationNotification` | public sealed class, `NotificationBase` | `Behaviors\GroupManipulationNotification.cs` |
| `GroupPhase` | public enum: `Begin`, `Apply`, `End`, `Cancel` | `Behaviors\GroupManipulationNotification.cs` |
| `GestureMath.Conjugate` | internal | `Core\Interaction\Recognizers\GestureMath.cs` |
| `NotificationExtensions.DispatchInto` | gains a `DeliveryClass` parameter, `FanOut` by default | `Core\Interaction\NotificationExtensions.cs` |

- The group types are in `VL.Fu.Behaviors`, next to `SelectionNotification`, because they carry the `SelectionProvider` (N7 D2).
- `Movable` and `Transformable` keep the N6 shape: setters are fragments, the constructor is a `[Fragment]`, channels are compared with `ReferenceEquals`, and a swap during a gesture cancels it.

### 2. `Movable` (v1 §6.3, M1)

Pins: `SetPosition(IChannel<Vector2>)` in parent space (layout units, for `SetPosition` or a translation), `SetButtons(Left)`, `SetThreshold(4)`, `SetEnabled`. It hosts a `DragGestureRecognizer`.

- **Press:** freezes `Position` (`_positionAtPress`). If the node also has a `Transformable`, it warns once (M1).
- **Drag started:** requests `GestureKind.Drag` and returns false, like `Draggable`.
- **Granted, alone:** `Position = positionAtPress + OffsetInParent` on every move and on the Up (§7.1). The offset is measured from the press, so the node stays under the pointer.
- **Granted, in a group:** see §5. The driver writes nothing itself; it receives the deltas as a member like everyone else.
- **Cancelled** (another claim, Esc, device loss, a setter): `Position` returns to `positionAtPress` if the drag published. A setter cancel owes the release.

### 3. `Transformable` (v1 §6.3, §7.3, T3)

Pins: `SetTransformation(IChannel<Matrix>)`, `SetAllow(bool translate = true, bool scale = true, bool rotate = true)`, `SetEnabled`. It hosts a `DragGestureRecognizer` (left button, 4 DIP) and a `PinchGestureRecognizer` (4 DIP), and combines them (F4):

| Gesture | Delta `D` in the node's parent space | Claim |
|---|---|---|
| One pointer drags | `T(offsetInParent)`; identity if translate isn't allowed | `Drag` |
| Two pointers pinch | `T(center) · R(rotation) · S(scale) · T(−centerAtPair)`. Without translate, `center = centerAtPair`; without scale, `S = 1`; without rotate, `R = 1` | `Pinch`, both pointers |

- The second pointer going down (`Paired`) cancels the drag recognizer and keeps the value the drag wrote. The pinch starts from there: the transformation and `P` are frozen again.
- The `Pinch` request replaces the node's own `Drag` claim (higher kind). The `PointerCancel` that follows names a claim it is a member of, so it doesn't reset.
- When one of the pinch pointers lifts, the pinch ends. The remaining pointer does nothing until a new press, as in `Pinchable`.
- **TransformOrigin compensation (§7.3):** at the gesture start it freezes `M = Transformation` and `P = T(layoutPosition + pivot)`, `pivot = (TransformOrigin + 1) / 2 · size`, from the node's `Layout` and `TransformOrigin`. Then `M' = P⁻¹ · D · P · M`.
- **Cancelled:** `Transformation` returns to the value frozen at the start of the gesture.
- It warns once if the node also has a `Movable` (M1).

### 4. `GroupTransaction` and `GroupManipulationNotification` (v2 §7.2)

`GroupTransaction`
| Member | Meaning |
|---|---|
| `Provider` | The `SelectionProvider` whose selection is manipulated |
| `IsMember(owner)` | Whether `owner` joined at `Begin` |
| internal `Begin/Apply/End/Cancel(notification, …)` | Called by the driver, the behavior holding the claim. Each queues one notification into the provider node's subtree with `DispatchInto` |

`GroupManipulationNotification(transaction, phase, deltaInWindow)`
| Member | Meaning |
|---|---|
| `Transaction`, `Phase` | Which transaction, and what to do |
| `Concerns(node)` | `Begin` only: `node`'s nearest provider is the transaction's provider, and a `Selectable` on `node` reports `IsSelected` |
| `Join(owner)` | `Begin` only: makes `owner` a member and freezes the receiving node's `WindowToParent` for it. False if the space is scaled to zero |
| `TryGetDeltaInParent(owner, out Matrix delta)` | `Apply` only: the delta in the member's parent space, `windowToParent · deltaInWindow · windowToParent⁻¹`, with the matrix frozen at `Join` |

| Phase | Delivery | Who acts |
|---|---|---|
| `Begin` | Fan-out into the provider node's subtree (displayed nodes) | `Concerns(node)` and `Join(this)`; it freezes its value |
| `Apply` | Control into the provider node's subtree, one pass | Members: value = `deltaInParent` applied to the value frozen at `Begin` |
| `End` | Same | Members forget the transaction |
| `Cancel` | Same | Members restore the value frozen at `Begin` |

- **Delta in window space** (D1). The driver sends `deltaInWindow`, measured from the press. Each member converts it through its own `WindowToParent` frozen at `Join`, which is §7.2's `providerToParent · deltaInProvider · providerToParent⁻¹` with the provider's matrix cancelled out. It is the same math, and nobody needs the provider's matrices.
- **Members apply with v1 §7.2:**
  - `Movable`: `Position = deltaInParent · positionAtBegin`, a point mapping. The node's top-left moves, so items spread apart under a scale (S6).
  - `Transformable`: `Transformation = P⁻¹ · deltaInParent · P · transformationAtBegin`, with `P` frozen at `Begin` (§3).
- Control delivery reaches hidden members, so a member hidden mid-drag still gets `End` or `Cancel`.
- A member detached, disabled, or removed from `SetBehaviors` is no longer reached. It keeps its latest value and stops taking part; the rest continue (O5, D3).

### 5. Who drives

| Start | Driver | Claim node |
|---|---|---|
| Drag on an item with `Movable` or `Transformable` whose node is selected under an enabled provider | That behavior | The item |
| Pinch on a selected item with `Transformable` | That behavior | The item |
| Drag on the provider's background starting inside `SelectionBounds` (N7 D6) | `SelectionProvider` | The provider |

- **On grant**, the driver asks the provider for a transaction (`SelectionProvider.TryBeginGroup`). The provider holds one transaction at a time. If one is running, the new driver gets none and publishes nothing for this gesture (D5).
- **Moves:** `Apply(deltaInWindow)`. A drag sends a translation by the pointer's window offset. A pinch sends the pinch delta mapped from the item's parent space into window space with `GestureMath.Conjugate` and the frozen `parentToWindow`.
- **Up:** a last `Apply`, then `End`.
- **Cancel** of the driver (another claim, Esc, device loss, its own setter): `Cancel`. A cancel from a setter is owed, like the claim release: it is sent with the next notification of the pointer (D4).
- **The provider is disabled** mid-transaction: it marks the transaction cancelled. The driver sends the `Cancel` with its next notification of that pointer and resets (D4).
- A driver whose node is not selected, has no provider, or whose provider is disabled moves alone (§2, §3).
- Pressing an unselected item selects it with `Replace` on the Down (N7), so by the grant the drag moves a group of one, or the extended group with Shift.
- Clicking a selected item without a drag still collapses the selection (N7). With a drag, the item's `Selectable` gets the `PointerCancel` of the drag claim and doesn't collapse.

### 6. `SelectionProvider` changes

- A press inside `SelectionBounds` starts a **group drag** instead of nothing, with the same drag recognizer and buttons as the marquee. Outside, it is still the marquee. The kind is decided at the press. The group drag works in both modes and doesn't depend on `SetMarquee`.
- Group drag phases: `Started` requests `Drag`; on grant `TryBeginGroup`, then `Begin`; `Moved` sends `Apply`; `Ended` sends `Apply` and `End`; cancel sends `Cancel`.
- `SetEnabled(false)` cancels its own group drag like the marquee, and marks a running item-driven transaction cancelled.

## Decisions

| # | Decision |
|---|---|
| D1 | The delta travels in window space, and each member maps it through its own `WindowToParent` frozen at `Join`. Equivalent to v2 §7.2. It works when an item drives, since an item can't see the provider's matrices. |
| D2 | `Apply`, `End` and `Cancel` are one control pass into the provider's subtree; members filter with `IsMember`. One pass per member would hit the 64-pass outbox guard on a canvas. |
| D3 | A member that leaves is simply not reached any more and keeps its latest value. No direct `End`: `Movable` and `Transformable` have no idle facts to write. A direct `End` for patched members is deferred. |
| D4 | A group cancel that comes from a setter (driver or provider) is owed and sent with the next notification of the driver's pointer, like the owed release. |
| D5 | One transaction per provider. A second driver during it publishes nothing. |
| D6 | "Selected" for `Concerns` is read from the `Selectable` on the same node. Selection stays owned by `Selectable` (N7). |
| D7 | `Movable` and `Transformable` restore their value at press on cancel. That is their idle fact (B7), like `Draggable`'s zero offset. |
| D8 | A group pinch on the provider's background, and mouse rotation, are deferred. A group transform comes from pinching a selected `Transformable`. |
| D9 | `SelectionBounds` isn't refreshed during a group move. The patch applies the new values to the nodes on the next frame, so the bounds catch up with the next pointer input over the provider. |

## Tests

`VL.Fu.Tests\Behaviors\MovableTests.cs`, `TransformableTests.cs` and `GroupManipulationTests.cs`, through `BehaviorScene`.
- `Movable` alone: dragging writes `positionAtPress + offset`; a move within the threshold writes nothing; Esc restores the position at press; swapping the channel mid-drag restores the old one; under a scaled parent the offset is in parent units.
- `Transformable` alone: a drag translates; translate not allowed leaves it; a pinch about the center for origins (0, 0) and (−1, −1) keeps the pinch center fixed on screen; scale and rotate flags; a pinch replacing the drag doesn't reset; cancel restores.
- Group, item-driven: dragging a selected item moves every selected item; an unselected item pressed and dragged moves alone (Replace); Shift-press and drag moves the extended group; an item selected after `Begin` doesn't move; an item deselected mid-drag keeps moving until `End`; Esc restores every member; a hidden member gets `Cancel`; items under different parents (Child in Front) get the delta in their own parent space.
- Group, provider-driven: a drag inside `SelectionBounds` moves the group; outside it still draws a marquee.
- Group transform: pinching a selected `Transformable` scales the other selected `Transformable` about the pinch center; a `Movable` member's position spreads (S6).
- The provider disabled mid-drag cancels the group with the next move.
- A second drag on another selected item during a group drag moves nothing.
- `Movable` + `Transformable` on one node warns.
- A click on a selected `Movable` item without a drag still collapses the selection.

## As built

_Filled in after implementation._

## Deferred

- A direct `End` for patched members that leave mid-transaction (D3).
- Group pinch on the provider background, rotation with the mouse (D8).
- Keyboard nudge (arrows) for the selection.
- Snapping and constraints (axis lock with Shift).

## Risk

- `Concerns` walks up to the provider for every item on `Begin`, O(items × depth) once per gesture. Fine for UI trees.
- `Movable` moves the node's top-left, not its center, so a group scale spreads items from their corners. That matches v1 S6 but may look off for big items; `Transformable` is the answer there.
- A patch that doesn't apply `Position` or `Transformation` back to the node sees the values but no movement. The help patch has to show the wiring.
