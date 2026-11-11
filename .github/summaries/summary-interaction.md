# Interaction: summary and implementation plan

The final design for pointer interaction in Fu, and the order to build it in.

**Where decisions come from:**
- The decision log is [review-interaction-architecture.md](../proposals/review-interaction-architecture.md): A1–A10, tiers, and G/H/C.
- Kept from [proposal-interaction-01-general.md](../proposals/proposal-interaction-01-general.md): D1–D7 and the touch-promotion findings.
- Kept from [proposal-interaction-functional-pipeline.md](../proposals/proposal-interaction-functional-pipeline.md): typed results, publishing only after arbitration, and the lifecycle rules (§2.2).

**Superseded:** [proposal-interaction.md](../proposals/proposal-interaction.md) and [proposal-interaction-architecture.md](../proposals/proposal-interaction-architecture.md). Proposals 01 and the functional pipeline are superseded wherever they differ from this file.

Implementation tasks are cut from §9, one task file per phase, in the style of [tasks-render-composition.md](../tasks/tasks-render-composition.md).

---

## 1. Architecture

**A recognizer decides what happened. A behavior decides where it goes in the patch. The arbiter decides who gets it. The hit walk decides which nodes are under the pointer.**

| Concern | Owner | Kind |
|---|---|---|
| Device input to one pointer value | `PointerEvent`, built once by the root | `readonly struct` |
| Which nodes are under the pointer | the hit walk in `Interactable<TSelf>` | node base |
| Who gets the pointer: candidates, claims, capture, cancel, hover | `PointerArbiter` | internal class, one per root |
| What happened: thresholds and phases | recognizers (`ClickRecognizer`, `DragRecognizer`, `PinchRecognizer`, `WheelRecognizer`) | internal classes, no VL |
| The data of what happened | gesture structs (`ClickGesture`, `DragGesture`, `PinchGesture`, `WheelGesture`) | `readonly record struct` |
| Where it goes in the patch | behaviors: the VL nodes of tiers 1–3 | `Behavior` subclasses |
| Shared policy for a subtree | providers: the tier 2 behaviors | `Behavior` subclasses |

```
SkiaRenderer -> root.Notify(notification, caller)
  PointerEvent.TryCreate          decode once: kind, pointer id, buttons, modifiers, window position, scaling, time
  Esc, MouseLost, LostFocus       -> arbiter.CancelAll()
  pointer captured?               yes: the capturing node's frozen spaces, no walk
                                  no:  hit walk -> hit path (root ... front-most) with each node's spaces
  arbiter
    1. process: every candidate recognizer gets the event, front-most node first
    2. resolve: apply the claim rules; losers get Cancel(), which resets them
    3. publish: survivors' behaviors read their recognizer's gesture and write channels
  hover: compare the hovered path with the last one -> OnHoverChanged
  return handled (§5.6)
```

| Tier | Members | Owns | Knows nothing about |
|---|---|---|---|
| 0. Recognizers | `ClickRecognizer`, `DragRecognizer`, `PinchRecognizer`, `WheelRecognizer` | One pointer state machine, its thresholds, its gesture struct | VL, channels, nodes, targets |
| 1. Headless | `Hoverable`, `Clickable`, `Draggable`, `Pinchable` | One recognizer, its settings pins, and fact channels | What the facts are used for |
| 2. Scopes | `SelectionProvider`, `CanvasProvider`, `IGroupManipulable` | A concern shared by a subtree | Individual item behavior |
| 3. Manipulators | `Selectable`, `Movable`, `Transformable` | Applying gestures to channels the patch owns | Routing and arbitration |

---

## 2. Decisions

### 2.1 From the review

- **G1 Occlusion is CSS-like.**
  - The hit path is the front-most displayed node under the pointer plus its ancestors, whether or not they have behaviors.
  - "Front-most" follows the draw order, through one ordered-children accessor shared by `Render` and the walk.
- **G2 Opting out.**
  - The `PointerEvents` style (`Auto` / `None`) makes a node and its subtree transparent to the pointer.
  - Each behavior has its own `SetEnabled`. There is no node-level Enabled.
- **G3 Hover.**
  - A node is hovered while it or a descendant is on the hit path.
  - While a pointer is captured, hover stays on the capturer's path.
- **G4 Keyboard:** none in the first pass. Esc cancels at the root.
- **G5 Capture:** per pointer, and it goes to the node. The mouse is pointer −1; touches use their `Id`.
- **G6 Thresholds** are in DIP: window pixels divided by `CallerInfo.Scaling`.
- **G7 One behavior, one node.** A behavior attached to a second node warns and is ignored there.
- **G8 Cancelling restores** the value at press. A node hidden or detached mid-gesture cancels its gestures.
- **Arbitration by kind:** see §5.3.
- **H1–H4 Tier 1 channels:** see §6.1. Every output is a channel the patch creates upstream.
- **H5:** no widgets in C#. Widgets are patches.
- **C1–C8 Canvas and selection:** see §6.2 and §6.3.
- **No `.vl` work in C#.** The user patches help files and widgets.

### 2.2 Settled from the functional-pipeline discussion

| # | Decision | Reason |
|---|---|---|
| F1 | Recognizers return a typed gesture struct through `Current`; no shared `Gesture` base. `Gesture.cs` is deleted. | A typed value is easy to read and to assert in tests. Every behavior knows its recognizer's type, so a base has no consumer. |
| F2 | `PointerEvent` stays. Recognizers never see a VL notification. | The root has to decode anyway for routing, capture, Esc and touch promotion. Decoding again per recognizer would repeat device rules in every recognizer. |
| F3 | Recognizers are mutable state machines: an enum plus a `switch`, every field at its idle value, and `Cancel()` returning to that value. No reducer, and no separate state object. | One type per family. The arbiter needs a live object per candidate anyway, and a losing candidate is reset by `Cancel()`. |
| F4 | Composing gestures from other gestures happens inside behaviors only, as private helpers. | Composition acts on gestures the arbiter has already accepted, so it is behavior logic. A composed gesture that must compete is its own primitive recognizer. |
| F5 | **Resolve, then publish.** Behaviors write channels only after the arbiter has resolved the event. | A loser can't publish a start it never gets. |
| F6 | **Settings are captured at press.** A change applies to the next interaction. `SetEnabled(false)` takes effect immediately and cancels. | A drag can't change its threshold or buttons halfway through. |
| F7 | **Swapping or disconnecting a channel cancels the active gesture first,** writing its end state to the old channel. | The old channel isn't left showing `IsDragging = true`. |
| F8 | **Publishing tolerates the tree changing.** Channel subscribers run synchronously and may detach nodes or disable behaviors. The arbiter iterates over a copy of the recipients and rechecks each one before publishing. | Prevents publishing to a recipient that left or was disabled during the same notification. |
| F9 | The interaction owns a manipulated value until release or cancel. A patch write during the gesture is overwritten on the next move; after the end, the patch's value wins. | The functional pipeline's Q7, option A. Option B, cancelling on an outside write, would cancel every drag in a patch that writes the channel every frame. |
| F10 | `Interactable` keeps its name. The behavior contract is the abstract `Behavior` class, and `SetBehaviors` takes `Spread<Behavior>`. There is no `IInteractionBehavior`. | Nothing outside `Behavior` attaches to a node. Renaming would be churn. |

### 2.3 Kept from proposal 01

| # | Decision |
|---|---|
| D1 | The arbiter is a lazy field on the root node, reached through `Root()`. On re-rooting, the old root's arbiter cancels the subtree, and a former root drops its own arbiter's state. |
| D2 | `PointerEvent` carries positions already mapped: window, frozen parent space and node space. Recognizers never touch a matrix. |
| D3 | Hiding and detaching cancel by **push**, from `OnParentChanged`, `EndStyle` and `Dispose`, with a cheap check at the top of `Notify` as a backstop. |
| D4 | The arbiter calls `Behavior.OnRecognized(recognizer)` and `OnCancelled(recognizer)`. The behavior reads its own typed field `_drag.Current`, with no casts. |
| D5 | `Notify` returns true while a candidate is `Possible` or a pointer is captured, and for any touch that hits the tree. A passive mouse move over nodes with no candidates returns false. |
| D6 | `SKMatrix` internally, the same type as `CallerInfo.Transformation` and `LocalTransformation`. Stride types on pins. |
| D7 | The tree is walked on Down, on Wheel, and on Move or Up only while nothing is captured. Captured pointers go straight to their node. |
| TP | **Touch promotion:** Windows turns an unhandled touch into a mouse click (`TouchInput.cs`). Returning true for touches sets `Handled` and prevents that. As a backstop, mouse input is ignored while a touch is down and for 100 ms after it lifts. |

### 2.4 Defaults for the remaining points

These are the defaults; each phase's review may revisit them.

| # | Point | Default |
|---|---|---|
| B2 | A channel swapped mid-gesture | Cancel first (F7). |
| B3 | A setting changed mid-gesture | Applies to the next press (F6). |
| B4 | Double click | **`ClickRecognizer` counts clicks itself,** for mouse and touch alike. It uses `Mouse.DoubleClickTime` (500 ms) and a DIP version of `Mouse.DoubleClickSize` (5 px). The root ignores `MouseClickNotification`. **This deviates from H2,** which said to use VL's click count. VL injects that notification after the release for count 1 but on the second *press* for count 2, so it arrives when the pointer slot is already released or still pending. It also only exists for the mouse. The timestamp comes in through `PointerEvent.TimeMs`, so tests need no clock. |
| B5 | `Clickable` and `Draggable` on one node | Drag outranks click. Once drag claims, the click is cancelled: `IsPressed` goes false and `OnClick` doesn't fire. |
| B6 | Pinch | Uses the first two pointers captured to the node; a third is ignored. When either lifts, the pinch ends, and the remaining finger doesn't resume a drag until it is pressed again. Rotation is unwrapped by adding each step's angle change, wrapped to (−π, π]. When a second finger lands during a drag on the same node, the pinch claims and the drag is cancelled (G8 applies). |
| B7 | Cancel for headless facts | No `OnCancel` channel. A cancel writes the idle facts (`IsDragging = false`, `Offset = 0`, `IsPressed = false`) and doesn't fire `OnDragEnd` or `OnClick`. |
| B8 | `Draggable.Offset` | In layout units of the parent space frozen at press. It keeps its value after the drag ends, is written as 0 when the next drag starts, and is 0 after a cancel. |
| S1 | Scope lookup | `IInteractable.TryGetBehavior<T>(out T)` on each ancestor, walking up from the owner every time it's needed. O(depth), nothing cached. |
| S2 | `Selectable` acting on press | `ClickRecognizer`'s `Pressed` phase counts as pending feedback. `Selectable` selects on it without claiming, so a drag on the same item still wins. |
| S3 | Finding the selection | Walk the provider's subtree when it's needed: on press, at the end of a marquee, and for bounds. The walk stops at nodes that hold another `SelectionProvider`, so an inner provider hides its subtree from the outer one. Nothing registers. |
| S4 | `View` and `TransformOrigin` | The canvas composition sets `SetTransformOrigin(-1, -1)` on the content node, so `View` maps content space to canvas space about the top-left. The help patch shows this. The provider does no compensation. |
| S5 | Zoom clamp | `View` is scale plus translation only; the canvas never rotates. The scale is `M11`. |
| S6 | Group transform pivot | The pinch center. Under a group scale, `Movable` items spread apart (M1). |
| S7 | The patch writing during a gesture | F9. |
| S8 | Selection bounds | The axis-aligned bounds of each selected item's box mapped into provider space, combined. |

---

## 3. Types and files

```
Core/Interaction/
├── Interactable.cs           IInteractable, Interactable<TSelf>: behaviors, PointerEvents, hit walk, root Notify
├── PointerEvent.cs           PointerEventKind, PointerEvent, conversion from notifications
├── PointerArbiter.cs         pointer slots, candidates, claims, capture, cancel, hover path
├── HitPath.cs                HitPathEntry (node + frozen matrices), the reused path buffer
├── Recognizers/
│   ├── IRecognizer.cs        IRecognizer, GestureKind, Participation
│   ├── ClickRecognizer.cs    + ClickGesture, ClickPhase
│   ├── DragRecognizer.cs     + DragGesture, DragPhase
│   ├── PinchRecognizer.cs    + PinchGesture, PinchPhase
│   └── WheelRecognizer.cs    + WheelGesture
└── Behaviors/
    ├── Behavior.cs           base node: enabled, owner, recognizer list, channel helpers
    ├── Hoverable.cs  Clickable.cs  Draggable.cs  Pinchable.cs
    ├── Selectable.cs  SelectionProvider.cs  IGroupManipulable.cs
    ├── Movable.cs  Transformable.cs
    └── CanvasProvider.cs
Core/Styles/PointerEventsStyle.cs        PointerEvents enum + record
Styles/PointerEventsNodes.cs             SetPointerEvents
```

Gesture structs share their recognizer's file: a small type that belongs to one class.

`IInteractable` no longer extends `VL.Skia.IBehavior`. VL reaches the root through `ILayer.Notify` on `Renderable`, which forwards to `Interactable`'s root routing.

---

## 4. Contracts

These are sketches: doc comments are left out, and `// ...` marks bodies phase tasks will fill in.

```csharp
internal enum PointerEventKind { Down, Move, Up, Wheel, Cancel }

internal readonly struct PointerEvent
{
    public const int MousePointerId = -1;

    public PointerEventKind Kind { get; init; }
    public int PointerId { get; init; }
    public MouseButtons Buttons { get; init; }        // touch counts as Left
    public Keys Modifiers { get; init; }
    public long TimeMs { get; init; }                 // from the root's Stopwatch; tests pass plain values
    public float Scaling { get; init; }               // CallerInfo.Scaling: window pixels per DIP
    public Vector2 PositionInWindow { get; init; }    // pixels
    public Vector2 PositionInParent { get; init; }    // parent space frozen at press (A1)
    public Vector2 PositionInNode { get; init; }      // node space, for hit tests and press positions
    public float WheelDelta { get; init; }            // Wheel only, in notches

    // Called by the root, once per notification, before mapping.
    public static bool TryCreate(INotification notification, CallerInfo caller, long timeMs, out PointerEvent e) { /* ... */ }

    // A copy with the positions mapped for one node.
    public PointerEvent For(in SKMatrix windowToParent, in SKMatrix windowToNode) { /* ... */ }
}
```

```csharp
internal enum GestureKind { Click = 1, Drag = 2, Pinch = 3, Wheel = 100 }   // Wheel stands apart (§5.3)
internal enum Participation { Ignore, Possible, Claim }

internal interface IRecognizer
{
    GestureKind Kind { get; }
    bool IsIdle { get; }
    Participation Process(in PointerEvent e);   // an active gesture keeps returning Claim; Ignore once it is done
    void Cancel();                              // back to the idle values in the field initializers
}

internal sealed class DragRecognizer : IRecognizer
{
    private DragPhase _phase = DragPhase.Idle;
    private int _pointerId = PointerEvent.MousePointerId;
    private MouseButtons _buttons = MouseButtons.Left;        // setting
    private MouseButtons _buttonsAtPress = MouseButtons.None; // captured at press (F6)
    private float _thresholdDip = 4f;                         // setting
    private float _thresholdDipAtPress;                       // captured at press (F6)
    private Vector2 _startInParent, _startInWindow, _positionInParent;

    public DragGesture Current => new(_phase, _startInParent, _positionInParent, _positionInParent - _startInParent);

    public void SetButtons(MouseButtons buttons) => _buttons = buttons;
    public void SetThresholdDip(float thresholdDip) => _thresholdDip = thresholdDip;

    public Participation Process(in PointerEvent e) => _phase switch
    {
        DragPhase.Idle    => OnIdle(in e),     // Down with a matching button: capture settings, go Pending, Possible
        DragPhase.Pending => OnPending(in e),  // Move past the threshold: Started, Claim; Up: Idle, Ignore
        DragPhase.Started or DragPhase.Moved => OnActive(in e),  // Move: Moved, Claim; Up: Ended, Ignore
        _ => Participation.Ignore,
    };

    public void Cancel() { /* restore every field above to its initializer value */ }
}

internal readonly record struct DragGesture(DragPhase Phase, Vector2 StartInParent, Vector2 PositionInParent, Vector2 OffsetInParent);
```

```csharp
public abstract class Behavior : IDisposable   // [ProcessNode] on the leaves
{
    private bool _enabled = true;
    private IInteractable? _owner;

    public bool Enabled => _enabled;
    public void SetEnabled(bool enabled = true) { /* compare; on false: CancelAll() */ }

    // Read by the arbiter at press; no allocation.
    internal abstract int RecognizerCount { get; }
    internal abstract IRecognizer GetRecognizer(int index);

    // Called by the arbiter after resolving (F5), only for recognizers that survived.
    internal abstract void OnRecognized(IRecognizer recognizer);
    internal abstract void OnCancelled(IRecognizer recognizer);
    internal virtual void OnHoverChanged(bool isHovered) { }

    internal bool TryAttach(IInteractable owner) { /* G7: warn and refuse a second owner */ }
    internal void Detach() { /* CancelAll(), then clear the owner */ }

    protected IInteractable? Owner => _owner;
    protected void Write<T>(IChannel<T>? channel, T value) { /* null: skip; otherwise EnsureValue */ }
    protected void Fire(IChannel<Unit>? channel) { /* null: skip; otherwise SetValueAndAuthor(Unit.Default, author) */ }
}
```

Channel setters follow one pattern: compare with the current channel; if it changed, cancel the active gesture (F7) and store the new one.

```csharp
public void SetOffset(IChannel<Vector2>? offset = null)
{
    if (ReferenceEquals(offset, _offset))
        return;
    CancelActive();   // writes the idle facts to the old channels
    _offset = offset;
}
```

```csharp
public interface IGroupManipulable
{
    void Begin(in SKMatrix providerToParent);    // freeze the value at press and the mapping
    void Apply(in SKMatrix deltaInProvider);     // §7.2
    void End();
    void Cancel();                               // restore the value at press
}
```

`IInteractable` gains:
- `IReadOnlyList<Behavior> Behaviors`;
- `PointerEvents PointerEvents`;
- `bool TryGetBehavior<T>(out T behavior)`;
- `bool TryGetNodeToWindow(out SKMatrix nodeToWindow)`. This one walks up through `LocalTransformation` to the root's caller transformation, cached from the last `Notify` or `Render`.

---

## 5. Routing and arbitration

### 5.1 Spaces

For each node on the hit path:
- `parentToWindow` is the caller's transformation before the node's own `LocalTransformation`;
- `nodeToWindow` is the same with `LocalTransformation` applied;
- the arbiter stores both inverses: `windowToParent` and `windowToNode`.

When a pointer is pressed, the inverses are frozen for every candidate's node, and the captured pointer's later events are mapped with them. A drag offset in `PositionInParent` therefore excludes the node's own scale and rotation, and stays correct while the node moves (A1).

A node whose matrix can't be inverted is skipped by the walk, and an active gesture on it is cancelled.

### 5.2 Hit walk

- The walk starts at the root and visits children in reverse draw order through `OrderedChildren`, the same accessor `Renderable.Render` iterates forward.
- It skips a node when:
  - it has no layout;
  - its display is `None`;
  - its `PointerEvents` is `None` (the whole subtree is skipped);
  - its matrix can't be inverted.
- For a point to reach a node's children, it must be inside the node's `ClipArea` (when set), and inside a child's box for that child. Every bounds test is half-open: `[0, W) × [0, H)`.
- The walk ends at the deepest hit. The hit path is that node and its ancestors, root first, in a reused buffer, so the walk doesn't allocate.
- Task 07 (`SetClipRect`) adds one more gate; task 05 (z-index) only changes `OrderedChildren`.

### 5.3 Claim rules

- **Candidates:** on Down, every recognizer of every enabled behavior on the hit path whose `Process` returns `Possible` or `Claim`. They are kept in a flat list per pointer slot, front-most node first.
- **Kinds rank Pinch > Drag > Click.** A claim:
  - cancels lower kinds on the same node;
  - cancels equal or lower kinds on other nodes;
  - lets equal kinds on the same node proceed. `Clickable` and `Selectable` both fire on one click.
- **Front-most nodes process first.** When two nodes claim on the same event, the front-most one wins.
- **Wheel is separate.** It never enters a slot and never captures. Each Wheel event goes to the front-most node on the hit path that has an enabled `WheelRecognizer`.
- **Button filters:** a recognizer only becomes `Possible` for its own buttons. The left-button marquee and right-button pan can therefore share a node.

### 5.4 Capture and multiple pointers

- The first claim captures the pointer to the claiming node. Its later Move and Up events skip the walk and go to every recognizer on that node, mapped with the matrices frozen at press.
- A second pointer pressed on a node that already holds a capture joins it with the same frozen matrices, so a pinch sees both fingers in one space.
- A pointer never inherits another pointer's capture.
- Up releases that pointer's slot. A slot that is pending with no claim is cancelled on Up, after the recognizers have seen the Up, so a click can still claim on release.

### 5.5 Hover

- The hover path follows the mouse pointer or, failing that, the primary touch. It is recomputed by every walk.
- `OnHoverChanged(true/false)` is called only for nodes entering or leaving the path, so the cost is O(depth).
- While any pointer is captured, the path stays on the capturer's ancestors. Release recomputes it.
- Cancelling or hiding a subtree removes its nodes from the path immediately.

### 5.6 What `Notify` returns

D5 and the touch-promotion rule. The root also records `caller.Transformation` and `Scaling` for `TryGetNodeToWindow`.

### 5.7 Cancellation sources

| Source | Hook | Scope |
|---|---|---|
| Esc key down | root `Notify` | all slots |
| `MouseLostNotification`, `LostFocusNotification` | root `Notify` | mouse slot or all |
| Node detached or reparented | `Interactable.OnParentChanged` → old root's arbiter `CancelSubtree(node)` | subtree |
| Display `None` or `PointerEvents` `None` committed | `Interactable.EndStyle`, comparing with the last committed values | subtree |
| Node disposed | `Interactable.Dispose` | subtree; a root drops its arbiter |
| Behavior removed by `SetBehaviors` | `Behavior.Detach` → arbiter `CancelBehavior` | that behavior |
| `SetEnabled(false)` | `Behavior` | that behavior |
| Channel swapped or disconnected | channel setter | that behavior (F7) |
| A higher kind claims | arbiter | the cancelled candidates |
| Matrix not invertible | arbiter, when it validates a slot | that slot |

Every source runs the same path:
1. call the recognizer's `Cancel()`;
2. call `OnCancelled`, which writes the idle facts and makes manipulators restore their value at press;
3. remove the candidates;
4. release the capture;
5. fix up the hover path.

### 5.8 Publishing

Per notification:
1. All candidates process the event.
2. Claims are resolved and losers cancelled.
3. A copy of the surviving `(behavior, recognizer)` pairs is published in front-to-back order. Before each pair, the arbiter checks that the behavior is still enabled, still has the same owner, and its owner is still reachable (F8).

`IsPressed` and select-on-press count as pending feedback: they are published while a candidate is only `Possible`.

---

## 6. Behaviors

### 6.1 Tier 1: headless behaviors

| Behavior | Settings | Channels | Writes | On cancel |
|---|---|---|---|---|
| `Hoverable` | `SetEnabled` | `IsHovered: IChannel<bool>` | Hover enter and leave | `IsHovered = false` |
| `Clickable` | `SetButtons` (Left), `SetThreshold` (6 DIP: moving this far cancels the click), `SetEnabled` | `OnClick`, `OnDoubleClick: IChannel<Unit>`, `IsPressed: IChannel<bool>` | Pressed: `IsPressed = true`. Released inside: `IsPressed = false`, `OnClick`, then `OnDoubleClick` on the second click within the window (B4) | `IsPressed = false` |
| `Draggable` | `SetButtons` (Left), `SetThreshold` (4 DIP), `SetEnabled` | `Offset: IChannel<Vector2>`, `IsDragging: IChannel<bool>`, `OnDragStart`, `OnDragEnd: IChannel<Unit>` | Started: `Offset = 0`, `IsDragging = true`, `OnDragStart`. Moved: `Offset`. Ended: final `Offset`, `IsDragging = false`, `OnDragEnd` | `Offset = 0`, `IsDragging = false` (B7, B8) |
| `Pinchable` | `SetEnabled` | `Scale`, `Rotation: IChannel<float>`, `Translation`, `Center: IChannel<Vector2>`, `IsPinching: IChannel<bool>` | Totals since the pinch started; `Center` in the frozen parent space | Idle totals (1, 0, 0), `IsPinching = false` |

- Pin order: `Behavior` (25) for the channel and settings pins, and `Enabled` (100) for `SetEnabled`.
- Constructors take only the hidden `NodeContext`.
- Recognizers are created in field initializers.

### 6.2 Tiers 2 and 3: selection

**`Selectable`**
- Pins: `IsSelected: IChannel<bool>` (two-way), `SetEnabled`. It hosts a `ClickRecognizer`.
- **Press on an unselected item:** it selects the item and replaces the selection, or adds to it with Shift, or toggles it with Ctrl. It doesn't claim (S2).
- **Click on a selected item without a drag:** on release, the selection collapses to this item.
- **No provider in scope:** it warns once and toggles only itself.

**`SelectionProvider`**
- Settings: `SetMode(Single | Multi)` and `SetMarquee(enabled = true, buttons = Left)`.
- Outputs: `SelectionBounds` and `MarqueeRect` as `Optional<RectangleF>`. Both are in the provider's node space, are updated during `Notify`, and are read every frame.
- It holds a `ClickRecognizer` and a `DragRecognizer` for its own background:
  - **click:** clears the selection (C2);
  - **drag starting inside `SelectionBounds`:** moves the group (§7.2);
  - **drag starting outside:** draws a marquee, which selects the items whose bounds it fully contains (C3). Shift adds and Ctrl toggles.
- Single mode clears the other items' `IsSelected` only on user interaction. It stores no selection of its own.

### 6.3 Tiers 2 and 3: manipulation and canvas

**`Movable`**
- Pins: `Position: IChannel<Vector2>` in parent space, plus `SetButtons`, `SetThreshold` and `SetEnabled`. It hosts its own `DragRecognizer`.
- **Alone:** the position is the position at press plus `OffsetInParent`.
- **When its node is selected under a provider:** when the drag starts it calls the provider's group move, and the provider applies the move to every selected item through `IGroupManipulable` (C7).
- It warns when a `Transformable` is on the same node (M1).

**`Transformable`**
- Pins: `Transformation: IChannel<Matrix>`, `SetAllow(translate, scale, rotate)`, `SetEnabled`. It hosts a `DragRecognizer` and a `PinchRecognizer`, and combines them internally (F4).
- One finger translates; two fingers transform about the pinch center.
- It compensates for `TransformOrigin` (§7.3) and implements `IGroupManipulable`.

**`CanvasProvider`**
- Pins: `View: IChannel<Matrix>`, `SetPan(buttons = Right)`, `SetZoom(min, max, wheelStep)`, `SetEnabled`. It hosts a `DragRecognizer` (pan), a `WheelRecognizer` and a `PinchRecognizer`.
- The right button drags to pan. The wheel zooms about the cursor, and a pinch zooms about its center (C5). The zoom is clamped, and the canvas never rotates (S5).
- A right click without a drag does nothing (C4).

**The canvas is a patch composition:**

```
Canvas node      overflow hidden; CanvasProvider + SelectionProvider
└── Content node SetTransformOrigin(-1, -1), SetTransform(View)
    └── Items    Selectable + Movable (or Transformable)
```

---

## 7. Math

Notation: column vectors, so `A·B` applies `B` first. Stride multiplies in the opposite order, so convert at the pins.

### 7.1 Drag offset

```
startInParent   = windowToParentAtPress · pressInWindow
currentInParent = windowToParentAtPress · currentInWindow
offsetInParent  = currentInParent − startInParent
thresholdCheck  = |currentInWindow − pressInWindow| / Scaling ≥ thresholdDip
```

### 7.2 Group delta

- `deltaInProvider` is the gesture's delta in the provider's node space, frozen at press: a translation for a move, or scale and rotation about the pinch center for a transform.
- For each item, `providerToParent = windowToParentAtPress · providerToWindowAtPress`, captured in `Begin`.
- The delta in the item's parent space is the same delta expressed through that mapping:

```
deltaInParent = providerToParent · deltaInProvider · providerToParent⁻¹
Movable:       position      = deltaInParent · positionAtPress           (the pivot moves; items spread under a scale)
Transformable: transformation = compensated deltaInParent (§7.3) · transformationAtPress
```

### 7.3 `TransformOrigin` compensation

`Layoutable` composes the node's matrix as:

```
LocalTransformation = T(layoutPosition) · T(pivot) · M · T(−pivot)
pivot               = (origin + 1) / 2 · size        (origin in −1..1; 0 is the center)
```

A delta `D` applied in parent space must give `T(layoutPosition)·T(pivot) · M' · T(−pivot) = D · LocalTransformation`. So:

```
P  = T(layoutPosition + pivot)          frozen at press
M' = P⁻¹ · D · P · M
```

### 7.4 Canvas zoom about a point

With the content origin at (-1, -1) (S4), the content's local matrix is `T(contentPosition) · View`. Zooming by a factor `k` about the point `c` in canvas space:

```
D     = T(c) · S(k) · T(−c)
View' = T(contentPosition)⁻¹ · D · T(contentPosition) · View
k     = clamp(scale(View) · k, min, max) / scale(View)
```

- `c` is the cursor for the wheel and the pinch center for a pinch.
- `contentPosition` is the content node's layout position in the canvas.
- The provider reads `contentPosition` from its first child at the start of a gesture. That child is the content node by convention, and the help patch documents it.

---

## 8. Code rules

- **No initial state in constructors.** A node's constructor only passes the hidden `NodeContext` to its base. Every state field has an initializer that is its idle value, and `Cancel()` / `Reset()` returns to exactly those values. A test asserts that a new instance and a cancelled one are in the same state.
- **Settings:** `SetX` setters, called every frame. They compare before storing and are cheap when nothing changed. Getters follow `public X => _x`.
- **Readable code:**
  - state machines are an enum plus a `switch`;
  - use early returns;
  - name one local per math step;
  - name every matrix `fromToTo` and every point `xInSpace`, and give thresholds their unit (`thresholdDip`).
- **Per-event paths:** no LINQ, closures, Rx, C# events or reflection, and no allocation in steady state. Use reused buffers for paths and slots.
- **Channels:**
  - the patch owns every channel and the behavior never disposes one;
  - a `null` channel is skipped;
  - write values with `EnsureValue` and events with `SetValueAndAuthor`.
- **Facade:**
  - `IReadOnlyList<T>`, and `Spread<T>` only for VL pin groups;
  - `Optional<T>` instead of NaN;
  - nodes never throw; they warn with `LogWarning`, worded as in the style instructions;
  - doc comments have a multi-line `<summary>` and use `<remarks>` for anything surprising.
- **Tests** go in `VL.Fu.Tests\Core\Interaction\`, named `Member_Situation_Result`, and run without the VL runtime.
  - Recognizers are tested with plain `PointerEvent` sequences.
  - The arbiter and the walk are tested with laid-out trees and fake behaviors.
  - Tier 1–3 behaviors are tested with real channels.

---

## 9. Implementation plan

Each phase is one task file: `.github\tasks\task-interaction-NN-*.md`. A phase is done when the solution builds and `VL.Fu.Tests` passes.

| # | Phase | Contents | Key tests | Depends on |
|---|---|---|---|---|
| I1 | Plumbing | `OrderedChildren`, used by `Render` (no behavior change); `PointerEvents` enum, record, staging and `SetPointerEvents` node; `IInteractable` drops `IBehavior`, and `Renderable.Notify` forwards to the root; delete `Gesture.cs` | style node tests; render order unchanged; `PointerEvents` staged and committed | none |
| I2 | `PointerEvent` | `TryCreate` for mouse down/move/up/wheel, touch, `MouseLost`, `LostFocus`, Esc; DIP from `Scaling`; ignores `MouseClickNotification`; `For(...)` mapping | conversion of each notification kind; touch becomes Left; wheel notches; `For` maps through both matrices | I1 |
| I3 | Hit walk | `TryBuildHitPath` with reverse order, `ClipArea`, half-open tests, skip rules and per-node spaces; `TryGetNodeToWindow`; `TryGetBehavior<T>` | front-most wins; clipped child not hit; display and `PointerEvents` `None` skipped; rotated and scaled nodes; right and bottom edges excluded; no allocation | I1 |
| I4 | Behavior base | `Behavior`, `IRecognizer`, `GestureKind`, `Participation`; `SetBehaviors` (Spread, pin order 25) with the G7 owner check; channel helpers; a fake recognizer for tests | second owner warns; removing a behavior detaches and cancels; `Fire` delivers equal events twice | I1 |
| I5 | Arbiter | Pointer slots, candidates, claim rules, capture, frozen spaces, resolve then publish, publishing with a copied list, every cancel source in §5.7, hover path, `Notify` return value, touch-promotion guard | ancestor drag cancels a child click; equal kinds proceed; capture continues outside the node and skips front siblings; a second pointer doesn't inherit capture; a detach or hide without input cancels; Esc cancels; hover stays frozen while captured; a subscriber detaching mid-publish is safe | I2, I3, I4 |
| I6 | Recognizers | `ClickRecognizer` (press, release, cancel on move past threshold, its own click count), `DragRecognizer`, `WheelRecognizer`, `PinchRecognizer` (B6) | each transition table; settings captured at press; `Cancel` returns to the idle values; mouse and touch sequences give the same phases | I4 |
| I7 | Headless behaviors | `Hoverable`, `Clickable`, `Draggable`, `Pinchable` with the §6.1 channels | §6.1 writes and cancel writes; a channel swap cancels first; disabling cancels; click and drag on one node | I5, I6 |
| — | Milestone | The user patches headless widgets and help files | | I7 |
| I8 | Selection | `Selectable`, `SelectionProvider`: select on press, collapse on release, clear, marquee, modes, `SelectionBounds` and `MarqueeRect`, nested providers | the canvas interaction table, row by row; marquee fully-contains; Ctrl and Shift; no provider in scope | I7 |
| I9 | Move | `Movable`, `IGroupManipulable`, group move from an item drag and from inside the bounds | press an unselected item and drag moves only it; dragging a selected item moves the group; cancel restores every item; items under different parents | I8 |
| I10 | Transform | `Transformable` with `SetAllow`, origin compensation and group transform | pinch about the center for each origin; delta correct under a zoomed parent; the `Movable` + `Transformable` warning | I9 |
| I11 | Canvas | `CanvasProvider`: pan, wheel zoom, pinch zoom, clamp | zoom keeps the cursor point fixed; clamp; an item drag after a zoom moves by the right amount | I7; I9 for the combined trace |
| I12 | Docs | `summary-core-inheritance.md` and this file's status | | I11 |

**Links to the render tasks:**
- Task 05 (z-index) needs I1 and changes only `OrderedChildren`.
- Task 07 (`SetClipRect`) adds a gate to the I3 walk.
- Task 08 (scrolling) needs I7 for wheel and drag input.

---

## 10. To verify during implementation

| Item | Where | Phase |
|---|---|---|
| Touch promotion: does returning true on touch stop the synthesized mouse events? Check on a touch device. | `TouchInput.cs`, `SkiaRenderer.Win32.cs` | I5 |
| `Mouse` injects `MouseClickNotification` (`injectMouseClicks` defaults to true): confirm it reaches `Notify`, and that ignoring it loses nothing | `Mouse.cs:64`, `SkiaInputDevices.cs` | I2 |
| `Channel.Create`, `EnsureValue` and `SetValueAndAuthor` work in tests without the VL runtime | VL.Core channels | I4 |
| What `CallerInfo.Scaling` means (window pixels per DIP) and whether the notification `Position` is in client pixels | `CallerInfo`, `SkiaRenderer` | I2 |
| Frame order: setters during the mainloop step, `Notify` and `Render` from the message pump, all on the main thread | `SkiaRenderer.cs` (proposal 01 §3.0) | I5 |
| Whether `MouseWheelNotification.WheelDelta` is in notches or raw units of 120 | VL.Core notifications | I2 |

---

## 11. Not planned, or later

- Keyboard focus, `KeyPressable`, Tab navigation (G4).
- Key-based selection (`Selectable<T>`, a provider-level `IChannel<Spread<T>>`).
- Snapping, context menus (C4), long press, double tap tuning, undo.
- Public gesture outputs, gesture graphs built in the patch, replay (functional pipeline Q2 B and Q4 B).
- Configurable arbitration rules (functional pipeline Q5 B).
- Widgets in C# (H5); the overlay (render task 06).
- Renaming `Interactable` to `Interactive`.
