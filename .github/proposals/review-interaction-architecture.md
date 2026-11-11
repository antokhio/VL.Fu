# Review: Interaction Pipeline Architecture

Review of `proposal-interaction-architecture.md` (referred to as "the proposal", sections as §n) against the current code
(`Interactable.cs` is a stub whose `Notify` returns false) and the VL sources in `VL.StandardLibs`.

Verified to exist as the proposal uses them: `MouseLostNotification`, `LostFocusNotification`, `KeyboardLostNotification`,
`TouchNotification.Primary`/`Id`, `INotification.WithSender`, `IWorldSpace2d`, `IProjectionSpace`, `SpaceHelpers.DoMapFromPixels`,
`IChannel.IsValid`/`EnsureValue`, and the protected `HasLayout`, `IsDisplayed`, `ClipArea`, `LocalTransformation` on `Layoutable`.

## What to keep

- Tree-native `Notify` in reverse draw order, mirroring `Renderable.Render` (children back to front, then the layer, then behaviors).
- `Possible` → `Started` disambiguation with continue-and-`Cancel()` on lower-priority behaviors.
- Transient (passive) behaviors that observe without consuming.
- Cancelling behaviors that get unplugged from `SetBehaviors`, and on `MouseLost` / `LostFocus` / `KeyboardLost`.
- Clip-gated child routing through `ClipArea`, which matches what is drawn now that task 01 is done.
- Freezing the reference space when a drag starts.

---

## A. Correctness bugs

### A1. The drag freezes the node's own space, not the parent's (§1.2, §4.3)
The principle says the inverse of the parent's `CallerInfo.Transformation` is frozen. `Interactable.Notify` passes `innerCaller` to behaviors, which already includes the node's `LocalTransformation`, and `TryBeginPointer` inverts that.

Freezing still prevents feedback, but the deltas are measured in the node's own space as it was at press. Position and the translation part of `SetTransform` live in parent space, so:
- a node scaled by 2 moves 50 px for a 100 px drag;
- a rotated node moves in the wrong direction.

**Fix:** hand behaviors both spaces, for example by passing the parent caller next to `innerCaller`, or by giving `DragGesture` a parent-space delta next to the local one. The "drag to move" case should use the parent space.

### A2. Gesture end states are inconsistent and can't be polled reliably (§3.3, §4)
There are three different behaviours for the same concept:

| Behavior | End of gesture |
|---|---|
| `Clickable`, `Draggable` | `Completed` stays until the next notification of any kind arrives, which then resets it to `Default`. |
| `Hoverable`, `KeyPressable`, every `Cancel()` | `Emit(Completed/Canceled)` then `Emit(Default)` synchronously. This is the "pulse trap" the research doc criticises in Hronomapper. |

Notifications arrive outside the frame update, so a frame can see none or several:
- `IsClicked` stays true for many frames when the mouse rests after a click, so a patch reacting to it fires repeatedly;
- `IsClicked` is never seen when a move arrives in the same frame;
- `State` never shows `Completed` or `Canceled` for hover, key and cancel;
- `IObservable` subscribers do see every state.

**Fix:** latch the end of a gesture per frame. Behaviors are process nodes, so an update fragment can publish what happened since the last frame (`Clicked` as a bang, `ClickCount`, last `Completed` gesture) and then clear it. Keep `State` as the live phase. Decide this before writing any behavior (see Q4).

### A3. When an ancestor claims an event, descendants above it are not cancelled (§3.4)
Children are notified first. When a parent behavior claims in step 3, it cancels only lower-priority behaviors on itself; children that already saw the event keep their `Possible` state.

Example: a parent `Draggable` (threshold 4 px) over a child `Clickable` (threshold 6 px).
1. A move of 5 px starts the parent's drag; the child is still `Possible`.
2. The user releases. The child completes the click and consumes the `MouseUp`.
3. The parent's `CancelSiblingAndSelfBehaviors` then cancels its own active drag.

The result is a click plus a cancelled drag, instead of a drag. Equal thresholds don't fully hide this either, since the drag starts at `>=` and the click cancels at `>`.

**Fix:** a claim cancels every other non-transient behavior that holds `Possible` in the tree, not only on the same node. Root-level tracking (A5) makes this cheap.

### A4. Sibling cancellation stops at the siblings' own behaviors (§3.4)
`CancelSiblingAndSelfBehaviors` cancels `children[j].Behaviors` of lower siblings, but not their descendants. A `Possible` click on a grandchild under a lower sibling survives a consumed event.

The same cancellation runs on every consumed event, including each drag `Updated` move, at every ancestor level.

### A5. Capture is not really capture (§3.4, §5)
`HasCapturedDescendant` only stops the clip gate from skipping a subtree. The captured behavior still sits in the normal order, so:
- higher siblings, their layers, and their own consumers see the event first, and any of them returning true starves the drag;
- moves outside the captured node still reach `Hoverable` on other nodes;
- the check walks the whole subtree for every child, on every event, for every clipped node.

The research doc (§3.4 there) proposed root-level capture: the root keeps `(node, behavior)` and delivers to it first. The proposal dropped this without saying why.

**Fix:** keep a capture slot on `Root`, delivering to the capturer first, with its frozen space, before the normal walk.

### A6. Gestures get stuck when a node leaves the walk mid-gesture
`Notify` returns early on `!HasLayout || !IsDisplayed`, and a detached node is no longer visited. If a node is set to display none, or removed from its parent, while its `Draggable` is `Started` or its `Clickable` is `Possible`:
- it never gets the `MouseUp` or the `MouseLost`;
- `HasCapture` stays true;
- the `IsDragging` channel stays true.

**Fix:** cancel the subtree's behaviors in `OnParentChanged` (when detached) and when display becomes none, or let root capture (A5) cancel a capturer that is no longer reachable.

### A7. Keyboard events go to every node (§3.4, §4.4)
Key notifications have no position, so `hasPosition` is false and `isInside` is true. Every `KeyPressable` in the tree fires, in reverse draw order. With `consume: true`, the frontmost node in draw order swallows the key, which has nothing to do with focus.

There is no focus model (see Q1).

### A8. The projection-space mapping is wrong (§3.1)
`LocalSpaceNotificationSender.MapFromPixels(…, out inNormalizedProjection, out inProjection)` re-normalizes into `boundsInPixels`:
- this is the axis-aligned box around the node, so it is wrong for rotated or skewed nodes;
- `DoMapFromPixels` divides by `size - 1`, so a box 1 px wide divides by zero, and 0 px gives a negative divisor;
- it silently changes the meaning of `PositionInProjectionSpace` for every VL.Skia layer hosted inside a node.

VL.Skia's own `IntermediateTransformNotificationSender` passes the original projection positions through and only remaps world space.

**Fix:** do the same: pass projection positions through and drop `boundsInPixels`.

### A9. Ancestors' transient behaviors miss events a child consumed (§3.4)
When a child returns true, the parent returns before step 3, so a `Hoverable` on the parent doesn't see the event. This contradicts principle 1.3 that transient behaviors observe everything.

During a child drag, the parent's hover freezes. If the pointer leaves the parent mid-drag, it stays hovered until the next unconsumed move.

### A10. Plain `IBehavior`s only receive events inside the node (§3.4)
Non-`IInteractionBehavior` behaviors, such as stock VL.Skia behaviors, are notified only while `isInside`. They never see a `MouseUp` outside the node, so their own drag or press state gets stuck. VL.Skia behaviors expect every event and do their own hit testing.

---

## B. Mismatches with the codebase

| # | Item | Issue |
|---|---|---|
| B1 | Diagram (§2) | Draws `NodeBase → Layoutable → Interactable → Renderable`. The real chain is `NodeBase → Traversable → Stylable → Layoutable → Interactable → Renderable → RegionProvider`, closed by `IFuNode`. The `Children`/`Root` the walk relies on come from `Traversable`. |
| B2 | Doc comments | One-line summaries (`/// <summary>Latest gesture state for mainloop polling.</summary>`, `/// <summary>Optional channel sink…</summary>`) break the multi-line convention. |
| B3 | Fragments | With `FragmentSelection.Explicit`, `SetPriority` and `SetGestureChannel` have no `[Fragment]`, so they never appear in VL. Constructors carry `[Fragment]`, which existing nodes don't (only `FuNodeSpectral` uses one, for `Order`). |
| B4 | Invalid input | `SetThreshold` clamps silently and `SetPriority` accepts anything. The convention is to `LogWarning` and ignore. `Clickable` has no `SetThreshold` at all (hard-coded 6). |
| B5 | `SetBehaviors` caching | `_rawBehaviors = behaviors as Spread<IBehavior>` is null for any non-`Spread` list, so the early-out never hits and the LINQ pipeline (`Where`/`Distinct`/`OrderByDescending`/`ToSpread`/`SequenceEqual`/`Contains`) runs every frame. Reuse the `Traversable.ToSpread` pattern and compare by reference. |
| B6 | Priority order | Sorting happens only when the spread changes; a later `SetPriority` leaves the order stale. Either re-sort when a priority changes (behaviors would need to know their owner) or make priority construction-only. |
| B7 | Interfaces | `IInteractable : ILayoutable, IBehavior` and `IRenderable : IInteractable, ILayer` (with `ILayer : IRendering, IBehavior`) declare `Notify` twice. It is harmless, but `IInteractable` could drop `IBehavior` once `IRenderable` exists, or keep it deliberately; say which. |
| B8 | `TryGetWorldPosition` | For an `INotificationWithPosition` that isn't a `NotificationWithPosition`, it falls back to the raw pixel `Position` and calls it local. This mixes spaces silently; it should return false instead. |
| B9 | `HitTest` | Inclusive on both edges (`<=`), so two adjacent siblings both hit on their shared edge. Use half-open `[0, W) × [0, H)`. |

---

## C. Missing pieces

- **Focus model (A7):** which node receives keys; how focus moves (click, tab); `GotFocusNotification`/`LostFocusNotification` per node.
- **One behavior on several nodes:** nothing stops connecting one `Clickable` to two `FuNode`s, which then share a state machine and capture. Track the owner and warn when it is attached to a second node.
- **Cancel semantics for values:** a cancelled drag leaves `_valueChannel` at the last dragged value. Define whether cancelling restores `_valueAtStart`.
- **Wheel:** `WheelScrollable` is listed in §7 and `WheelGesture` exists, but there is no specification. It feeds task 08 (scroll), so it decides how a wheel event is claimed in nested scroll areas.
- **Multi-touch:** only `Primary` touches are handled. `PinchGesture` is declared without a behavior, and VL's `GestureNotification` (zoom/pan/rotate) is ignored.
  - Verify whether the renderer also promotes touch to mouse events; if so, `Clickable` fires twice per tap.
- **Cancel broadcast:** the research doc's in-band `GestureCancelledNotification` (from Calibrator) was dropped. Direct `Cancel()` calls work in-tree but not for behaviors hosted inside layers.
- **Render roadmap interplay:**
  - Notify must iterate exactly the order Render draws in, so z-order (task 05) and `ChildrenLayer` (task 04) need one shared ordered-children accessor;
  - `SetClipRect` (task 07) must gate hit testing the way `ClipArea` does;
  - effects (task 03) need a rule on whether an invisible node is hit-testable.
- **Layer content versus behaviors:** `NotifyContent` runs before the node's own behaviors, and if it consumes, the node's behaviors are cancelled. A VL.Skia widget drawn in a node's layer therefore beats the node's `Clickable`. This may be intended, but should be stated (Q3).
- **Thresholds are in window pixels:** 6 px is 3 DIP at 200 % scaling. Measure in DIP, or in the frozen reference space.

---

## D. Performance

- **Whole tree on every event:** children are not hit-tested before recursing; only the parent's clip gates them. A tree without overflow hidden is fully visited on every mouse move.
  - Subtree bounds would allow pruning. A node's own `Bounds` is not enough, since unclipped descendants can spill.
- **Allocations per visited node per event:** `WithSender` creates a new notification, plus its `Lazy` and closure, plus a sender. Each behavior then allocates a `with` record and pushes it through a `BehaviorSubject`.
  - **Fix:** compute the local point by matrix math while walking (no allocation), and wrap the notification lazily, only when a layer or behavior actually receives it.
- **Repeated walks:** `HasCapturedDescendant` and `CancelSiblingAndSelfBehaviors` walk subtrees on every consumed or clipped event (A4, A5).

---

## Recommendations (in order)

1. **Root capture slot** on `Root`: the capturer receives first, with its frozen reference space; a claim cancels all other `Possible` behaviors tree-wide; an unreachable capturer is cancelled. Fixes A3–A6 and removes `HasCapturedDescendant`.
2. **Pass behaviors both spaces** (node-local for hit position, parent for deltas). Fixes A1.
3. **Frame-latched gesture outputs** with a single, documented end-state rule. Fixes A2.
4. **Pass projection space through** like `IntermediateTransformNotificationSender`, and wrap notifications lazily. Fixes A8 and most of D.
5. **Let transient behaviors on ancestors see consumed events** (A9), and **notify plain `IBehavior`s outside the node** too (A10).
6. **Focus model** before `KeyPressable` (A7).
7. **Conventions pass:** B2–B9.

## Open questions

- ~~**Q1 Focus.**~~ Resolved: deferred; see G4.
- ~~**Q2 Capture scope.**~~ Resolved: per pointer, to the node; see "Composition rules".
- ~~**Q3 Layer versus behaviors.**~~ Resolved: no VL.Skia widgets inside a node's layer (see Decisions).
- ~~**Q4 Gesture outputs for the mainloop.**~~ Resolved: `IChannel<Unit>` for events (see Decisions).
- ~~**Q5 Cancelled drag.**~~ Resolved: restore the value at press.
- ~~**Q6 Transient on ancestors.**~~ Resolved: see G3.

---

## Decisions

- **Q3: a node's layer is purely visual.** No VL.Skia widgets live inside it, so the layer is never notified.
  - `NotifyContent` goes away, and so do A10 and the "layer content versus behaviors" item in C.
  - `SetBehaviors` takes Fu behaviors only, so plain `IBehavior`s are no longer accepted.
  - `IInteractable` drops `IBehavior` (B7). The root's `Notify` comes from `ILayer` through `IRenderable`.
  - Nothing reads `PositionInWorldSpace` downstream any more, so there is no reason to wrap notifications with `WithSender`. A8 and the allocation part of D go away: the walk maps the point by matrix.
- **Q4: events are published through `IChannel<Unit>`; states through `IChannel<bool>` / `IChannel<Vector2>`.**
  - Channels fire synchronously on each transition, so nothing has to be polled and A2's per-frame problem goes away.
  - `State`, `IObservable<TGesture>`, `BehaviorSubject` and `SetGestureChannel` go away.
  - Event channels must be written with `SetValueAndAuthor(Unit.Default, …)`. `EnsureValue` compares with the current value and would never fire, since `Unit` always equals `Unit`.

---

## SRP and simplification

**SRP is not maintained in the proposal.** Behavior and gesture are one class (`Behavior<TGesture>`), and arbitration is split between `Interactable` and every behavior.

| Concern | Where the proposal puts it | Problem |
|---|---|---|
| Mouse/touch → pointer | Every behavior (`HandleTouch`, touch switches, `Primary` checks) | Duplicated input normalization in each behavior. |
| Gesture recognition (thresholds, phases) | `Behavior.OnNotify` | Mixed with VL pins, channels and arbitration return values. |
| Gesture data | `Gesture` records | Each record is both live state and emitted event; `Gesture.State` names a phase. |
| Arbitration (claim, capture, cancel) | `Interactable` (`CancelSibling…`, `HasCapturedDescendant`, priority loop) **and** each behavior (`return true`, `HasCapture`, `IsTransient`, `Priority`) | No single owner, which is the root cause of A3–A6. |
| Space freezing | `Draggable._captureInverse` | Each behavior re-implements a routing concern, and gets the space wrong (A1). |
| Publishing | `State` + `IObservable` + gesture channel + per-value channels | Three outputs for one fact. |
| Applying the result | `Draggable` writes `_valueAtStart + offset` into a value channel | Application logic inside an input behavior. |

### Proposed split: one owner per concern

| Unit | Kind | Single responsibility |
|---|---|---|
| `PointerEvent` | `readonly struct` | One pointer event: kind (Down/Move/Up/Cancel), pointer id, buttons, modifiers, position in node space and in the frozen reference space. Built once, at the root, from mouse and touch notifications. |
| `Interactable<TSelf>` | Node base | **Routing**: walks children in reverse draw order, maps the point into node space by matrix, gates by `ClipArea`, hit-tests, and yields the hit path. Holds the behavior list. No gesture logic. |
| `PointerArbiter` | Plain class, owned by the root | **Arbitration**: collects candidate gestures along the hit path on Down, freezes the reference space, gives capture to the first gesture that claims, cancels every other candidate tree-wide, and cancels a capturer whose node left the tree or was hidden. Fixes A1, A3–A6. |
| `ClickGesture`, `DragGesture` | Plain classes, no VL, no channels | **Recognition**: a state machine fed `PointerEvent`s. It answers `Ignore` / `Possible` / `Claim` and exposes read-only data (start, current, delta). It can be tested without VL. |
| `Behavior` → `Clickable`, `Draggable` | VL process nodes | **Binding**: owns one gesture, forwards its settings pins (`SetButtons`, `SetThreshold`, `SetEnabled`), and turns its transitions into channel writes (`OnClick: IChannel<Unit>`, `IsPressed: IChannel<bool>`, `Delta: IChannel<Vector2>`). No routing, hit testing or arbitration. |
| `Hoverable` | VL process node, no gesture | Hover is not arbitrated: it is whether the node is on the arbiter's current hit path. That removes `IsTransient`. |

**Behavior is behavior, gesture is gesture:** a gesture decides *what happened*, a behavior decides *where it goes in the patch*, the arbiter decides *who gets it*, and `Interactable` decides *which nodes are under the pointer*.

### What disappears

- `Behavior<TGesture>`, `BehaviorSubject`, `IObservable`, the `State` pin, the public `Gesture` records and `GestureState` (an internal phase enum stays in each gesture).
- `IsTransient`, `HasCapture`, `Priority` and `GesturePriority`: within one node, ties go by spread order; along the path, front-most wins.
- `LocalSpaceNotificationSender`, `NotifyContent`, `HasCapturedDescendant`, `CancelSiblingAndSelfBehaviors`.
- `KeyPressable` is deferred until a focus model exists (Q1). `Draggable`'s value channel is removed: the patch adds `Delta` to its own value, or a later `Movable` behavior does.

### File layout

```
Core/Interaction/
├── Interactable.cs        routing, hit test, behavior list
├── PointerEvent.cs        struct + conversion from mouse/touch notifications
├── PointerArbiter.cs      capture, candidates, cancel
├── Gestures/
│   ├── IGesture.cs        Process(in PointerEvent) → Ignore | Possible | Claim; Cancel()
│   ├── ClickGesture.cs
│   └── DragGesture.cs
└── Behaviors/
    ├── Behavior.cs        VL node base: enabled, owner check, channel helpers
    ├── Clickable.cs
    ├── Draggable.cs
    └── Hoverable.cs
```

### Questions on the simplification

- ~~**S1:** Drop `Priority` in favour of spread order on a node?~~ Superseded by arbitration by gesture kind (see Behavior tiers).
- ~~**S2.**~~ Resolved: hover is derived from the hit path; see G3.
- ~~**S3.**~~ Resolved: see H1.

---

## Behavior tiers

Four tiers. Each one depends only on the tiers below it, and only tiers 1–3 are VL nodes.

| Tier | Members | Owns | Knows nothing about |
|---|---|---|---|
| 0. Gestures (internal C#) | `ClickGesture`, `DragGesture`, `PinchGesture`, `WheelGesture` | The pointer state machine and thresholds | VL, channels, nodes, targets |
| 1. Headless input | `Hoverable`, `Clickable`, `Draggable`, `Pinchable` | One gesture, its settings pins, and fact channels passed in from the patch (see H1–H3) | What the facts are used for |
| 2. Scopes (on a container) | `SelectionProvider`, `CanvasProvider`, `IGroupManipulable` | A concern shared by a subtree: who is selected; the view and the common space | Individual item behavior |
| 3. Manipulators | `Selectable`, `Movable`, `Transformable` | Applying gestures to channels the patch defines and feeds in | Routing and arbitration |

### Decisions

- **T1 Selection identity:** on the first go, selection is per item through `Selectable.IsSelected: IChannel<bool>`. Keys (`Selectable<T>`, a provider-level `IChannel<Spread<T>>`) come later.
- **T2 Group move:** yes. Dragging a selected item moves every selected item in the same `SelectionProvider` (see below).
- **T3:** Transformable only: no `Scalable` / `Rotatable`. The patch creates the channel, feeds it into the behavior, and applies its value to the node itself, through a style or a pin.
- **T4:** marquee is built into `SelectionProvider` and on by default.
- **M1:** `Movable` stays, as a separate, lightweight implementation. It is not meant to be combined with `Transformable` on one node, and warns if both are attached.
- **M2:** group operations cover both: a selection moves and transforms together.
- **C1:** right drag pans; for the full left-button map see "Canvas interaction".

### Composition rules

- **Tier 3 reuses tier 0, not tier 1.** `Movable` hosts its own `DragGesture`; it doesn't take a `Draggable` as input. One node in the patch for one intent, with no references between behavior nodes.
- **Manipulators write patch-owned channels and never call node setters.** The patch sets `SetTransform`/`SetPosition` every frame and would overwrite them; the channel is the single source of truth.
- **Arbitration by gesture kind, not `Priority`:**
  - Kinds rank `Pinch > Drag > Click`. `Wheel` is its own kind, and the front-most node that wants it gets it. Hover is not arbitrated.
  - A claim cancels lower kinds on the same node and equal-or-lower kinds on other nodes.
  - Equal kinds on the same node all proceed. `Clickable` + `Selectable` on one node both fire on one click.
  - Front-most nodes get each event first.
  - Gestures only become `Possible` for their own buttons. Marquee on the left button and pan on the right button can therefore share a node.
- **Capture is per pointer and goes to the node, not to a gesture:**
  - every gesture on the capturing node sees all of that node's pointers, so `Pinch` can take over from `Drag` when a second finger lands;
  - mouse is pointer −1, and touches use their `Id`.
- **Scopes are found lazily:** a tier 3 behavior walks up from its owner node to the nearest node holding the scope it needs, each time it needs it. The walk is O(depth) and leaves no cache to go stale on reparenting.

### Canvas: a separate `CanvasProvider`, not part of `SelectionProvider`

The two are independent, and each is useful without the other:
- a pan/zoom map has no selection;
- a selectable list has no pan/zoom.

Merging them would put viewport state, view gestures and selection state in one node, which is the SRP problem this review is trying to remove.

| | `SelectionProvider` | `CanvasProvider` |
|---|---|---|
| State | None of its own: the selection is the set of `Selectable`s in its subtree whose `IsSelected` is true | None of its own: the view lives in the patch's channel |
| Gestures | Click on background (clear), drag on background (marquee, or group move inside the selection bounds) | Right drag (pan), wheel zoom about the cursor, pinch |
| Output | Writes `IsSelected` of its `Selectable`s | `View: IChannel<Matrix>`, which the patch applies with `SetTransform` on a content node |
| Space | Tests the marquee against items in its own node space | Defines the common space: the content node's space |

**A canvas** is a patch composition, not a new class:

```
Canvas node      overflow hidden; CanvasProvider + SelectionProvider
└── Content node SetTransform(View)
    └── Items    Selectable + Movable (+ Transformable)
```

**No extra item behaviors are needed for a canvas:**
- `Movable` already measures deltas in the frozen parent space (A1), which is the content space, so dragging is correct at any zoom.
- Canvas-specific item behaviors, like snapping to a grid, can come later as tier 3.

### Canvas interaction

| Input | Where | Result | Owner |
|---|---|---|---|
| Right drag | Anywhere | Pan | `CanvasProvider` |
| Wheel | Anywhere | Zoom about the cursor | `CanvasProvider` |
| Left press | Unselected item | Select it, replacing the selection | `Selectable` (acts on press) |
| Left click (no drag) | Selected item | Collapse the selection to this item | `Selectable` (acts on release) |
| Left drag | Item | Move the selection (which now contains the item) | `Movable` → `SelectionProvider` |
| Left click | Background | Clear the selection | `SelectionProvider` |
| Left drag | Background, inside the selection bounds | Move the selection | `SelectionProvider` |
| Left drag | Background, outside the selection bounds | Marquee | `SelectionProvider` |
| Ctrl / Shift + click or marquee | | Toggle / add instead of replace | `Selectable`, `SelectionProvider` |

All of this falls out of the arbitration rules without special cases:
- items are in front, so their rules win over the background;
- click versus drag is the threshold;
- left versus right is the gestures' own button filter.

`Selectable` acting on press, and not only on click, is what lets "press an unselected item and drag" move just that item. It also lets "press a selected item and drag" keep the whole selection.

### Group move and transform

The `SelectionProvider` is the single coordinator, whether the drag starts on an item or on the selection bounds:
- a drag on the bounds is the provider's own `DragGesture`;
- a `Movable` or `Transformable` whose node is selected hands its gesture to the provider when the gesture starts, instead of applying it alone.

To keep tier 2 independent of tier 3 (dependency inversion), the provider drives an interface, which `Movable` and `Transformable` implement:

```
IGroupManipulable
    Begin()                            freeze the value at press and the provider → parent mapping
    Apply(Matrix deltaInProviderSpace) Movable: moves its pivot by the delta (translation only, so items spread apart under a group scale);
                                       Transformable: applies the full delta about its frozen pivot
    End() / Cancel()                   Cancel restores the value at press
```

- The delta is measured in the provider's node space with the reference frozen at press (A1). Each item maps it into its own parent space with the frozen provider → parent matrix, so the delta stays correct at any zoom, and for items under different parents.
- **Selection bounds** are the union of the selected items' bounds mapped into the provider's space, computed at press.
- **Headless outputs:** the provider draws nothing.
  - It exposes `SelectionBounds` and `MarqueeRect` as `Optional<RectangleF>` pins: empty when there is no selection or no marquee.
  - The patch draws them.

### Members

- **`SelectionProvider`**
  - `SetMode(single | multi)`. In multi mode, Ctrl toggles and Shift adds.
  - `SetMarquee(enabled = true, buttons = Left)`; the marquee selects the items whose bounds it fully contains (C3).
  - Single mode clears the other items' `IsSelected` on user interaction only. Values the patch writes into the channels are respected as they are.
  - A `Selectable` with no provider in scope warns and toggles only itself.
- **`Selectable`:** `IsSelected: IChannel<bool>`, two-way, so the patch can select too.
- **`Movable`:** lightweight. `Position: IChannel<Vector2>` in parent space, which is layout units, for `SetPosition` or a translation; it implements `IGroupManipulable`.
- **`Transformable`:** `Transformation: IChannel<Matrix>`. One finger translates and two fingers pinch about the pinch center, with the compensation for the node's `TransformOrigin` built in. `SetAllow(translate, scale, rotate)` flags; it implements `IGroupManipulable`.
- **`CanvasProvider`:** `View: IChannel<Matrix>`, `SetPan(buttons = Right)`, `SetZoom(min, max, wheelStep)`.

---

## Decisions by topic

### General handling (routing, arbitration, spaces)

| # | Decision |
|---|---|
| G1 | **Occlusion is CSS-like and z-order aware.** The front-most displayed node under the pointer, and its ancestors, form the hit path, whether or not they have behaviors; siblings drawn behind are not reached. "Front-most" follows the draw order, including z-index once task 05 lands, through one ordered-children accessor shared by `Render` and the walk. |
| G2 | **Opting out:** a `PointerEvents` style (`Auto` / `None`) makes a node and its subtree transparent to the pointer, so it is shareable through styles like any other instruction. Each behavior node has its own `SetEnabled`. There is no node-level Enabled. |
| G3 | **Hover follows the hit path, CSS-like:** a node is hovered while it, or any of its descendants, is on the hit path. While a pointer is captured, hover is frozen to the capturer's path. `Hoverable` holds no gesture. |
| G4 | **No keyboard in the first pass.** Esc cancels the active capture, through the root, with no focus needed. Focus and `KeyPressable` come later. |
| G5 | **Capture is per pointer and goes to the node** (Q2). Mouse is pointer −1, and touches use their `Id`. |
| G6 | **Thresholds are in DIP:** window pixels divided by `CallerInfo.Scaling`, so 6 means the same distance at any DPI. |
| G7 | **One behavior, one node:** a behavior attached to a second node warns and is ignored there. |
| G8 | **Cancelling restores** the value at press (Q5); a node hidden or detached mid-gesture cancels it (A6). |

To verify when implementing: whether the Skia renderer also promotes touch to mouse notifications. If it does, mouse events marked as promoted must be dropped, or a tap clicks twice.

### Headless behaviors (tiers 0–1)

Every output is a channel the patch creates upstream and passes in through a setter. The behavior writes it and never owns it. Event channels are written with `SetValueAndAuthor` (see Decisions).

| # | Behavior | Channels (set from upstream) |
|---|---|---|
| H1 | `Draggable` | `Offset: IChannel<Vector2>` (since press, in the frozen parent space), `IsDragging: IChannel<bool>`, `OnDragStart` / `OnDragEnd: IChannel<Unit>` |
| H2 | `Clickable` | `OnClick`, `OnDoubleClick: IChannel<Unit>` (from VL's `ClickCount`), `IsPressed: IChannel<bool>` |
| H3 | `Pinchable` | Totals since start: `Scale: IChannel<float>`, `Rotation: IChannel<float>`, `Translation: IChannel<Vector2>`, `Center: IChannel<Vector2>`, plus `IsPinching: IChannel<bool>` |
| H4 | `Hoverable` | `IsHovered: IChannel<bool>` |
| H5 | — | **No headless widgets** (toggle, slider…) in C# for now: widgets are patches built on tier 1 facts. |

### Canvas and selection (tiers 2–3)

| # | Decision |
|---|---|
| C1 | Right drag pans; the left-button map is in "Canvas interaction". |
| C2 | A left click on empty space inside the selection bounds clears the selection: a click is a click, and only a drag moves. |
| C3 | The marquee selects items whose bounds it fully contains (`RectangleF.Contains(RectangleF)`), tested in the provider's space. |
| C4 | A right click without a drag does nothing; it is reserved for a context menu. |
| C5 | The wheel zooms about the cursor and a pinch about the pinch center, clamped to `SetZoom(min, max, …)`. |
| C6 | Selection is per item through `Selectable.IsSelected` (T1); keys come later. |
| C7 | Group move and transform go through the `SelectionProvider` and `IGroupManipulable` (T2, M2). |
| C8 | `Movable` is lightweight and separate from `Transformable` (M1); `Transformable` replaces `Scalable` / `Rotatable` (T3). |

No questions are open. The next step is to rewrite `proposal-interaction-architecture.md` to this design, or to split it into an implementation task list in the style of `tasks-render-composition.md`.
