# Task 01: Clip children by Overflow

Source: summary §2, §7 row 1; proposal §3. Depends on: none.

## Goal

A node whose Overflow is Hidden or Scroll clips its children to its padding box. Its own `Layer` is not clipped, so backgrounds, border strokes and shadows drawn on the node itself stay whole, as in CSS (option C1).

## Design

* `Layoutable`: `protected unsafe RectangleF? ClipArea`, computed on access from the handle:
  * null when there is no layout, or `GetOverflow()` is `Visible`;
  * otherwise `(borderLeft, borderTop, width - borderRight, height - borderBottom)` from `GetComputedBorder(Left/Top/Right/Bottom)`, with right and bottom clamped to at least left and top.
  * Read in `Render`, not cached in `ReadLayout`: `ReadLayout` returns early when the box is unchanged, so an Overflow or Border change alone would be missed. The cost is one enum and four float reads per clipped node.
* `Renderable.Render`: after `_layer?.Render(inner)` and before the children:
  * `canvas.ClipRect(area.ToSKRect(), SKClipOperation.Intersect, antialias)`;
  * skip the children when the area is empty;
  * the existing `RestoreToCount(save)` pops the clip.
* `Bounds` stay the node's own box; overflow doesn't change them.
* `SetOverflow` docs and the `OverflowStyle` remarks drop "rendering doesn't clip yet".

## Decisions (settled)

1. **The node's own layer is not clipped**, only its children; `SetClipRect` (07) clips the layer too.
2. **Antialias is hybrid:** off while the canvas matrix only scales and translates, so axis-aligned clips stay pixel-crisp; on when it rotates or skews.
3. **Scroll clips like Hidden.** Yoga already gives Scroll its layout (children not shrunk to fit along the main axis); the content offset comes with task 08.

## Control

Everything goes through `SetOverflow`; there is no extra setter or pin. Visible draws everything, Hidden and Scroll cut the children at the padding box, so border setters move the clip edge and padding doesn't. Removing the setter brings back Visible on the next pass.

## Tests (`RenderableTests.cs`, pixel checks on a small `SKBitmap`)

* A child overflowing a Hidden parent is cut at the parent's edge; under Visible it isn't.
* Scroll clips like Hidden.
* The parent's own layer, drawn larger than its box, is not clipped.
* With a border, children are cut at the padding box, not over the border.
* Changing only Overflow, with no layout change, takes effect in the next render.
* A clip on a rotated node follows the rotation.

## Deferred

* `Notify` skips the children of a clipped node when the pointer is outside `ClipArea`; the node itself still hit-tests its full box. Comes with interaction.
* Rounded overflow clip and culling. The explicit rect clip is task 07; scrolling is task 08.
* Clipping through a mask: not planned. A geometric clip needs no offscreen buffer and can be hit-tested exactly.
