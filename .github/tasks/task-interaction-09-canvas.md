# Task interaction-09: Canvas

Source: [summary-interaction-v2.md](../summaries/summary-interaction-v2.md) §8, §14 N9; v1 [summary-interaction.md](../summaries/summary-interaction.md) §6.3, §7.3, §7.4, S4, S5, C4, C5, I11; [review-interaction-architecture.md](../proposals/review-interaction-architecture.md) "Canvas interaction". Depends on: [task-interaction-06](task-interaction-06-behaviors.md); reuses the pivot compensation of [task-interaction-08](task-interaction-08-move-transform.md).

## Goal

A container becomes a pannable, zoomable view of its content. After this task:
- `CanvasProvider` is a VL process node with the N6 shape. It writes one `View` channel that the patch owns and applies to the content node's transformation;
- the right button drags to pan, the wheel zooms about the cursor, and a two-touch pinch zooms about its center and pans with it;
- the zoom is clamped and the view never rotates (S5);
- the content keeps whatever `TransformOrigin` the patch gives it, and the point under the cursor or the touches stays under them (S4, v1 §7.4);
- a deeper node that consumes the wheel (an inner canvas, later a scroll view) wins, because the wheel bubbles from the hit node up.

The canvas is a patch composition, as in v1 §6.3:

```
Canvas node      overflow hidden; CanvasProvider (+ SelectionProvider)
└── Content node SetTransformation(View); any TransformOrigin, centered by default
    └── Items    Selectable + Movable (or Transformable)
```

## Design

### 1. Types and files

| Type | Kind | File |
|---|---|---|
| `CanvasProvider` | public sealed class, `NodeBase, IBehavior` | `Behaviors\CanvasProvider.cs` |
| `GestureMath.ComposeAboutPivot` | internal, moved out of `Transformable.Compose` | `Core\Interaction\Recognizers\GestureMath.cs` |
| `GestureMath.ScaleOf` | internal: the length of a matrix's X axis | same |

- `CanvasProvider` keeps the N6 shape: setters are fragments, the constructor is a `[Fragment]`, the channel is compared with `ReferenceEquals`, and a swap during a gesture cancels it.
- `Transformable` calls `ComposeAboutPivot` instead of its private copy. Nothing else changes for it.

### 2. Pins

| Pin | Default | Meaning |
|---|---|---|
| `SetView(IChannel<Matrix>)` | | The content's transformation. The patch applies it to the content node with `SetTransformation`. |
| `SetPan(MouseButtons buttons)` | `Right` | The buttons that pan. `None` turns mouse panning off. |
| `SetZoom(float min, float max, float wheelStep)` | `0.1`, `10`, `1.2` | The scale limits, and the factor one wheel notch zooms by. `wheelStep = 1` turns wheel zoom off. Values that aren't finite, a `min` that isn't positive, `min > max` or a `wheelStep` that isn't positive are ignored with a warning. |
| `SetEnabled(bool)` | `true` | Disabling cancels a running pan or pinch. |

### 3. Spaces

The provider is on the canvas node, so the samples it reads come with the canvas node's matrices, and the recognizers measure in the canvas node's **parent** space. The content lives in the canvas node's **own** space. The provider maps into that space itself:
- at a press, it freezes `windowToNode` from `sample.WindowToNode`, and `parentToNode = windowToNode · parentToWindow`;
- the pan offset is `windowToNode · current − windowToNode · press`, from window positions, like `Movable`'s group offset;
- the pinch center and its translation are mapped through `parentToNode`. The pinch scale is a ratio of distances and doesn't change.

The canvas node doesn't move while its content pans, so the frozen matrix stays right for the whole gesture.

### 4. The content and its pivot (S4)

- The content node is the canvas node's **first child**, in patch order (v1 §7.4). The help patch documents the convention.
- At the start of every gesture the provider reads the content's `IInteractionNode.TransformPivotInParent` (N8), which is in the canvas node's space. Without a child, the pivot is `(0, 0)`.
- Every change is applied with the N8 compensation: `View' = View · (T(−pivot) · D · T(pivot))`, in Stride order, so the content's `TransformOrigin` never shifts what is under the pointer.

### 5. Gestures

| Gesture | Delta `D` in the canvas node's space | Claim | Value it starts from |
|---|---|---|---|
| Pan: a pan button drags past 4 DIP | `T(offsetInNode)` | `Drag` | `View` at the press |
| Wheel: a vertical notch over the canvas | `T(c) · S(k) · T(−c)`, `c` the cursor in the canvas node's space | none | `View` now |
| Pinch: two touches, one moves past 4 DIP | `T(center) · S(k) · T(−centerAtPair)` | `Pinch`, both touches | `View` at pairing |

- **Clamp (S5):** `k = clamp(s · factor, lo, hi) / s`, where `s = ScaleOf(View)` at the start, `factor` is `wheelStep^notches` or the pinch scale, and the limits widen to include `s` (`lo = min(min, s)`, `hi = max(max, s)`). A view the patch set outside the limits doesn't jump back; it just can't go further out. `ScaleOf` is the length of the X axis, which is `M11` for an unrotated view (S5) and stays right if the patch rotated it.
- **Wheel:** a vertical wheel over the canvas is processed (returns true) even at a limit, so an outer canvas doesn't zoom instead. The horizontal wheel isn't used, and returns false. With `wheelStep = 1` the wheel isn't used either.
- **Pinch** ignores rotation (S5). It pans with its center, so the content stays under the fingers. A pinch whose touches both land on one item with a `Transformable` belongs to the item, which is deeper. A pinch with one touch on an item and one on the background belongs to the canvas, and its `Pinch` claim takes the first touch from the item's `Drag` (the item restores).
- **Pan** with the mouse and **pinch** with touches can't run together: a gesture that starts while the other is granted is cancelled without requesting. The wheel does nothing during either, but is still processed.
- A right click without a drag does nothing (C4).
- **Cancelled** (another claim, Esc, device loss, a setter): `View` returns to the value the gesture started from. A setter cancel owes the release, like the other behaviors.
- One-finger touch panning isn't in this task: a touch is the left button, which belongs to selection and items. Two fingers pan through the pinch.

### 6. Selection on the same node

`CanvasProvider` and `SelectionProvider` sit together on the canvas node and don't know about each other. They split by button: left for selection, marquee and group drags, right for the pan. A pinch outranks the marquee's `Drag`, so two touches on the background zoom and cancel the marquee. `SelectionBounds` isn't refreshed during a pan, the same as N8 D9.

## Decisions

| # | Decision |
|---|---|
| D1 | The content is the first child, read at the start of every gesture, never cached. |
| D2 | The provider works in the canvas node's own space and maps the recognizers' parent-space values itself (§3). The recognizers don't change. |
| D3 | The clamp limits widen to include the current scale, so a patched view outside them never jumps. |
| D4 | Pan and pinch exclude each other; the wheel waits for both. Each gesture starts from one frozen `View` and writes absolute values, so mixing them would lose changes. |
| D5 | The pinch pans with its center, and two fingers are the only touch pan. |
| D6 | A vertical wheel over an enabled canvas with wheel zoom is always processed, even at a limit. |
| D7 | `ScaleOf` reads the X axis length instead of `M11`. They are the same for the views this provider writes. |

## Tests

`VL.Fu.Tests\Behaviors\CanvasProviderTests.cs`, through `BehaviorScene`, with the provider on Front (the canvas) and Child (its first child) as the content.
- Pan: a right drag writes the offset; a right move within the threshold writes nothing; a left drag doesn't pan; `SetPan(None)` turns it off.
- Pan under a scaled canvas: the offset is in the canvas node's space.
- Wheel: one notch zooms by `wheelStep` and keeps the cursor point fixed on screen, for content origins (0, 0) and (−1, −1); the result is processed; repeated notches stop at `max` and at `min`; a view set outside the limits doesn't jump.
- Wheel: a deeper canvas consumes it and the outer one doesn't change; the horizontal wheel isn't processed.
- Pinch: zooms about the center and follows it, keeping both touch points under the touches; rotation is ignored; clamped.
- A canvas pinch with one touch on an item with a `Movable` takes the touch, and the item restores.
- A pinch starting during a pan, and a pan starting during a pinch, are ignored; the wheel during a pan doesn't change the view.
- Esc during a pan and during a pinch restores the view; disabling and swapping the channel during a pan restore it and release the pointer with the next notification.
- Invalid `SetZoom` values are ignored.

## As built

- `Behaviors\CanvasProvider.cs` as designed. The pinch is fed touches only, and the pan the mouse only: `PinchGestureRecognizer` pairs any two pointers, so without the filter a right press and a touch would make a pinch. This is D5 enforced, and it keeps the touch pan → pinch hand-off of `Transformable` out of the canvas.
- A pinch that pairs or starts while the pan is granted is cancelled; a pan that starts while the pinch is granted is cancelled. Neither requests (D4).
- `GestureMath.ComposeAboutPivot(matrix, pivotInParent, deltaInParent)` and `GestureMath.ScaleOf(matrix)` are new. `Transformable`'s private `Compose` moved there unchanged, so both behaviors apply a change about the pivot the same way.
- The view and the content's pivot are frozen at the grant, not at the press: nothing writes the view between the two, and the wheel waits while either gesture is granted.
- `BehaviorScene` got `Wheel` and `HorizontalWheel`. `CanvasProviderTests`: 20 tests, 27 runs. Esc is tested during a pan; the pinch's restore is tested through disabling, which goes the same way. The suite is 491 green.

## Deferred

- One-finger touch panning, and panning with the horizontal wheel or Shift+wheel.
- Kinetic panning and animated zoom.
- Fit-to-content and zoom-to-selection; the patch can write `View` itself.
- A content node chosen explicitly instead of the first child.

## Risk

- The first-child convention is invisible in the patch. A canvas whose first child isn't the content zooms about the wrong pivot. The help patch has to show it.
- A centered content origin with content that changes size moves the pivot, so a zoomed view shifts when the content grows (S4). That holds for any transformation, and the help patch shows it.
- A patch that doesn't apply `View` back to the content sees the values but no movement.
