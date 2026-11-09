# Task 08: Scrolling through SetTransform

Source: summary §1.1 (Transform), proposal §3. Depends on: 01 (clip), interaction for wheel and drag input.

## Goal

Scroll content inside a clipped container by translating the content node with `SetTransform`. There is no relayout per scroll step: the transform is applied after layout, in `LocalTransformation`. This matches how VL.Skia's `Transform` composes (`PreConcat`, absolute `SetMatrix`, `Bounds` mapped through the matrix).

## Shape in a patch

```
container: SetOverflow(Scroll or Hidden), fixed or flexible size
  content: SetTransform(Translate(-scrollX, -scrollY))
    items...
```

* The container clips (task 01) and the content moves inside the clip.
* The content's `Bounds` follow the offset, so a patch can tell what's visible.

## What Fu needs to add

1. **Scroll range.** Yoga has no content size, only `GetHadOverflow()`. Add a read-only `ContentArea` on `Layoutable`: the union of the children's layout boxes, plus the padding, in node space, computed in `ReadLayout` when children change layout.
   * The patch clamps the offset to `ContentArea.Size - ClipArea.Size`.
   * The clamp is one frame late, like the viewport, which is acceptable.
2. **Layout of scroll content.** Yoga is believed to measure the children of a `Scroll` container without a limit along the main axis, at least for columns, so content grows past the container instead of shrinking; Hidden lets children shrink to fit. Confirm with a test and document the outcome.
3. **Hit-testing.** `Notify` maps the pointer through the inverse of `LocalTransformation`, which already includes the transform, as `TransformUpstream.Notify` does. Comes with interaction.

## Caveats

* `SetTransform` follows "later wins" and replaces, it doesn't multiply. A content node that also animates its own transform needs the matrices multiplied in the patch, or the scroll moved to a wrapper node.
* A direct `SetTransformation` on the node and a transform style write the same value; the style commit wins when its value changes. Keep scroll on one of the two.
* Scrollbars and wheel or drag handling are patch-level or come later as Fu components; they are not part of this task.

## Decisions

1. Is `ContentArea` the right name and shape (a rect in node space, including padding), or should it be `ContentSize` only?
2. Should Fu later offer a container-level `SetScrollOffset` style? It would translate all children after the clip and not touch the content's own transform. Not needed now, but it removes the "replaces your transform" caveat.

## Tests

* A translated child inside a Hidden container is clipped at the container edge, and the layout is unchanged by the translation.
* `ContentArea` of a Scroll container with overflowing children is larger than its box; it is updated when a child grows; it is the node box when there are no children.
* A Scroll container's children are not shrunk along the main axis.
