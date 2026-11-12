# Task interaction-09b: Scroll

Source: [summary-interaction-v2.md](../summaries/summary-interaction-v2.md) §8, §14 N9b; [proposal-layoutable.md](../proposals/proposal-layoutable.md) §2 (scrolling is composition). Depends on: [task-interaction-09](task-interaction-09-canvas.md), whose content convention and wheel handling it shares. Replaces the `ContentArea` and `SetScrollOffset` ideas of [task-render-08](task-render-08-scrolling.md): the range comes from the content node, and the offset reaches it through its transformation.

## Goal

A clipped container scrolls its content with the wheel and with a touch drag. After this task:
- `Scrollable` is a VL process node with the N6 shape. It writes one `Offset` channel that the patch owns and applies to the content node as a translation;
- the vertical wheel scrolls vertically, the horizontal wheel and Shift + wheel scroll horizontally;
- a touch drags the content, and mouse buttons can be added;
- the offset stays in the range that keeps the content's box covering the viewport, read from the content node itself;
- nested scroll views hand over: a wheel at the limit, and a drag along an axis the inner view can't scroll, go to the outer one.

The scroll view is a patch composition, like the canvas:

```
Viewport node    overflow hidden or scroll; Scrollable
└── Content node SetTransformation(Translation(−Offset)); sized by its children
    └── Items    Clickable, Draggable, ...
```

## Design

### 1. Types and files

| Type | Kind | File |
|---|---|---|
| `Scrollable` | public sealed class, `NodeBase, IBehavior` | `Behaviors\Scrollable.cs` |
| `IInteractionNode.ClipArea` | internal, explicit: forwards `Layoutable.ClipArea`, which is protected | `Core\Interaction\INotificationRoot.cs`, `Core\Interaction\Interactive.cs` |

### 2. Pins

| Pin | Default | Meaning |
|---|---|---|
| `SetOffset(IChannel<Vector2>)` | | How far the content is scrolled, in the viewport's units: (0, 0) at the start, positive when scrolled right or down. The patch applies `Translation(−Offset)` to the content node. |
| `SetWheelStep(float step)` | `48` | How far one wheel notch scrolls, in the viewport's units. `0` turns the wheel off. A step that isn't finite or is negative is ignored with a warning. |
| `SetDrag(MouseButtons mouse, bool touch)` | `None`, `true` | Which pointers drag the content. Touches drag by default; mouse buttons don't, as on the desktop. |
| `SetEnabled(bool)` | `true` | Disabling cancels a running drag. |

### 3. Range

- The content is the viewport's **first child** (N9 convention). Its box is its `Layout`, which leaves its transformation out, so scrolling doesn't change the range.
- The viewport is the viewport node's `ClipArea`, its padding box; the whole box when its overflow is visible.
- Per axis: `low = min(0, content.Start − viewport.Start)`, `high = max(0, content.End − viewport.End)`. Content that fits has the range `[0, 0]` on that axis and doesn't scroll along it.
- The range is read when a gesture starts and at every wheel notch. Without a child, nothing scrolls.
- Like the zoom limits (N9 D3), the range widens to include the current offset, so an offset left outside when the content shrank doesn't jump; it can only move back towards the range.

### 4. Gestures

| Input | Change | Claim | Processed |
|---|---|---|---|
| Vertical wheel | `Offset.Y −= notches · step` | none | when the offset changed |
| Horizontal wheel | `Offset.X += notches · step` (a tilt to the right scrolls right) | none | when the offset changed |
| Shift + vertical wheel | `Offset.X −= notches · step` (turning down scrolls right, as browsers do) | none | when the offset changed |
| Drag past 4 DIP, with a pointer `SetDrag` allows | `Offset = OffsetAtPress − (current − press)`, in the viewport's space | `Drag` | while granted |

- **Wheel chaining.** A wheel that doesn't move the offset, because the view is at its limit or that axis doesn't scroll, isn't processed, so it bubbles to an outer scroll view or canvas. This differs from the canvas, which takes the wheel even at a zoom limit.
- **Drag direction.** When the drag starts, its dominant direction is checked: if the view can't scroll along that axis, the drag is cancelled without a request, and the pointer is left to an outer scroll view. After the claim, the drag follows both axes, each clamped.
- **Spaces.** As the canvas pan: `windowToNode` is frozen at the press, and the offset is the difference of the mapped window positions, so a scaled viewport scrolls in its own units.
- **Children.** Claims resolve innermost first, so an item's own drag (`Draggable`, `Movable`) wins over the scroll. A `Clickable` item is pressed until the scroll's `Drag` claim takes the pointer and cancels the click (v2 §6). An ancestor canvas pinch outranks the drag, takes the touch, and the scroll restores.
- The wheel during a drag is processed and changes nothing.

### 5. Cancel

As every N6 behavior: Esc, a lost pointer, another claim, disabling, or swapping the channel during a drag writes the offset at its start. A setter cancel owes the release to the next notification of that pointer.

## Decisions

| # | Decision |
|---|---|
| D1 | The output is a `Vector2` offset, not a matrix. Scrollbars and "scroll to" read and write the same channel, and the patch keeps the content's own transform under its control. |
| D2 | The range comes from the first child's `Layout` and the viewport's `ClipArea`. No `ContentArea` on `Layoutable` and no `SetScrollOffset` style. |
| D3 | One behavior for both axes. The axes follow the range: content that fits along an axis doesn't scroll along it, so there's no `ScrollableX`/`ScrollableY` and no axis pin. |
| D4 | The wheel chains at the limit and the drag hands over by direction, so nested scroll views work without configuration. |
| D5 | Touches drag by default, mouse buttons don't. |
| D6 | The trailing padding isn't added to the range (CSS adds it). |

## Tests

- Vertical wheel scrolls down and up by the step; horizontal wheel and Shift + wheel scroll X.
- At the limit the wheel isn't processed, and an outer `Scrollable` takes it; an axis without overflow doesn't scroll and isn't processed.
- `SetWheelStep(0)` turns the wheel off; invalid steps are ignored.
- Touch drag scrolls past the threshold, clamped at both ends; within the threshold nothing is written.
- Mouse drag does nothing by default and scrolls with `SetDrag(Left)`.
- A scaled viewport scrolls in its own units.
- A horizontal drag in a view that only scrolls vertically isn't claimed, and an outer horizontal view takes it.
- A child `Clickable` pressed and then dragged doesn't click; a child `Draggable` wins over the scroll.
- An offset outside the range after the content shrank doesn't jump at the next wheel.
- Esc, disabling and swapping the channel during a drag restore the offset.

## As built

- `Behaviors\Scrollable.cs` as designed, from the canvas's pan path. One `DragGestureRecognizer` takes the left button for touches plus the mouse buttons of `SetDrag`; a press is told apart by pointer first, since a touch counts as the left button.
- `IInteractionNode.ClipArea` forwards the protected `Layoutable.ClipArea` explicitly in `Interactive`.
- The range is read at the drag's start, where it also decides the direction hand-over, and at every notch. The offset at start is read at the grant.
- Wheel signs: vertical up scrolls up, a horizontal tilt to the right scrolls right, Shift + wheel down scrolls right. To confirm with a real mouse in the help patch.
- The dispatcher mutes the mouse while a touch is down, so the wheel during a drag is tested with a mouse drag.
- `BehaviorScene` got `Resize`, `AddChild`, `Clip` and a modifier on `Wheel`. `ScrollableTests`: 20 tests, 22 runs. The suite is 513 green.

## Deferred

- Inertia after a flick, and overscroll bounce: both need per-frame updates, which behaviors don't get.
- Scrollbars, as Fu components bound to `Offset`.
- Keyboard scrolling (arrows, Page Up/Down, Home/End), which needs focus.
- Scrolling a focused or selected item into view.

## Risk

- A content node whose transform isn't a plain translation (the patch scales it too) gets a range in the wrong units. The help patch puts scale on an inner node.
- The range is read from the last layout, so a content that grows during a drag is clamped to the old range until the next gesture.
