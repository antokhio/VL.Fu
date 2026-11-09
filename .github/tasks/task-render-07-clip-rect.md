# Task 07: SetClipRect style

Source: summary §2.2 B. Depends on: 01 (antialias rule, ClipRect placement).

## Goal

Clip a node's own layer and its subtree to a rectangle in node space. Rectangle only; round rect, path and mask clips go through `ChildrenLayer` and VL.Skia nodes.

## Design

* Record `ClipRectStyle(IReadOnlyList<IStyle> Inputs, RectangleF Rect) : Style<IFuNode>`. A non-finite rect or a negative size throws.
* Node `SetClipRect`, with `Optional<RectangleF>` checked for finite values and a size of at least 0; invalid values are ignored with a warning. Needs a `ValidateFinite(Optional<RectangleF>)` overload on `StyleNode`.
* Staged on `Renderable`, reset to none in `BeginStyle`, committed in `EndStyle`; later wins.
* In `Render`, after `Concat` and the opacity check and before the effect layer: `canvas.ClipRect(rect, Intersect, antialias)`.
  * In node space, so it follows rotation, unlike VL.Skia's `ClipRect`, which clips to the axis-aligned pixel box.
  * Intersects with the overflow clip of task 01.
* `Bounds` unchanged.

## Tests

* Cuts the node's own layer and its children.
* A rotated node clips along its rotated edges.
* Intersects with an Overflow Hidden clip.
* Removing the style restores full drawing.
* An invalid rect is ignored.

## Deferred

`Notify` rejects pointers outside the rect (interaction).
