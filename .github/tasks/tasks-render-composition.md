# Render composition: tasks

Cut from [summary-render-composition.md](../summaries/summary-render-composition.md), which backs [proposal-render-composition.md](../proposals/proposal-render-composition.md).

## Order

| # | Task | Depends on | Status |
|---|---|---|---|
| 01 | [Clip children by Overflow](task-render-01-clipping.md) | none | proposed |
| 02 | [Pin Skia 2.88 layer behaviour with tests](task-render-02-skia-spike.md) | none | proposed |
| 03 | [Effect styles: opacity, image filter, color filter, blend mode](task-render-03-effect-styles.md) | 01, 02 | proposed |
| 04 | [ChildrenLayer for Precompose and other layer wrappers](task-render-04-children-layer.md) | 01 | proposed |
| 05 | [Sibling z-index](task-render-05-zindex.md) | 04 | proposed |
| 06 | [Overlay / portal](task-render-06-overlay.md) | 05, interaction | deferred |
| 07 | [SetClipRect style](task-render-07-clip-rect.md) | 01 | proposed |
| 08 | [Scrolling through SetTransform](task-render-08-scrolling.md) | 01; input with interaction | proposed |

01 and 02 can run in parallel. The render order every task fits into:

```
Render(caller)
  root: capture viewport
  return if no layout or display none
  Save, Concat(local)                          (transform, incl. scroll offset, 08)
  return if opacity is 0                       (03)
  ClipRect(clip style) if set                  (07, cuts layer and children)
  SaveLayer(effect paint) if any effect        (03)
  Layer.Render(inner)                          (may draw ChildrenLayer, 04)
  unless ChildrenLayer already drew:           (04)
    ClipRect(padding box) if overflow != visible  (01, cuts children only)
    children in draw order                        (05)
  RestoreToCount(save)
```

`Notify` mirrors it in reverse once interaction lands. `Interactable.Notify` is still a stub, so every hit-testing part is deferred.

## Not planned

* Round rect and path clips, masks: use VL.Skia's `ClipRect`, `ClipPath` and `Mask` through `ChildrenLayer` (04). Geometric clips stay preferred over masks for clipping: no offscreen buffers, and exact hit-testing.
* `SkiaPaint` as an input: a `.vl` record C# can't type; would need a bridge patched in `VL.Fu.vl`.
* `Func<ILayer, ILayer>` composer (stateless in VL), feedback loop, paint hack.
* Culling (`QuickReject`), subtree bounds, a container-level `SetScrollOffset`.
* Backdrop blur: needs SkiaSharp 3; VL.Core.Skia ships 2.88.8.

## Corrections to the summary

* §2.2, caching the clip box in `ReadLayout`: `ReadLayout` returns early on an unchanged box, so an Overflow or Border change alone would leave it stale. Read it in `Render` (01).
* §5.1 and §5.2, blur cut by layer bounds and filters following rotation: not verified; task 02 settles them.
* §3.2, `[Fragment(Order = ...)]` sets pin order, not call order.
* Effect record values are Skia reference types: compared by reference, never disposed by Fu; the style instructions get that exception (03).
* Scrolling with `SetTransform`: a transform style replaces earlier ones, and a direct `SetTransformation` writes the same value; clamping needs a content extent Yoga doesn't provide (08).
* Taken as given: the `Precompose`, `SkiaPaint`, `ClipRect`, `ClipPath` and `Mask` internals come from `.vl` patches, out of scope here.
