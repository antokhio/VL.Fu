# Task 03: Effect styles: opacity, image filter, color filter, blend mode

Source: summary §3.2 E1-A, §7 row 2; proposal §4 E1. Depends on: 01 (render order), 02 (layer bounds).

## Goal

Fade, blur, tint or blend a node together with its whole subtree as one group, through ordinary Fu styles. The pins take the Skia objects that VL.Skia's `Blur`, `DropShadow`, `ColorMatrix` and similar nodes already output. This does what `Precompose` does (`SaveLayer(paint)`, draw, `Restore`) inside `Renderable.Render`, at no cost when unused.

## Records (`VL.Fu\src\Core\Styles`, `: Style<IFuNode>`)

| Record | Value | Default |
|---|---|---|
| `OpacityStyle` | `float Opacity`, 0 to 1 | 1 |
| `ImageFilterStyle` | `SKImageFilter ImageFilter` | none |
| `ColorFilterStyle` | `SKColorFilter ColorFilter` | none |
| `BlendModeStyle` | `SKBlendMode BlendMode` | `SrcOver` |

* `Build(IFuNode node)` calls an internal stage method on `Renderable<IFuNode>`, like `TransformStyle` does with `StageTransformation`.
* Later wins: a later filter replaces an earlier one. Chaining filters is the user's job (`SKImageFilter.CreateCompose`, VL.Skia's compose nodes).
* `Opacity` outside 0 to 1 or non-finite throws; `BlendMode` goes through `YogaEnums`-style `Enum.IsDefined` checking.
* The Skia objects are compared by reference and never disposed by Fu, since the producing VL.Skia node owns them. This is an exception to the immutable-values rule; add it to `style-instructions.md`.

## Nodes (`VL.Fu\src\Styles\EffectNodes.cs`)

* `SetOpacity`: `Optional<float>`, `ValidateFinite`, then a range check with the usual warning.
* `SetImageFilter`, `SetColorFilter`: `SKImageFilter?` and `SKColorFilter?`. Null means "not set here", so these pins don't use `Optional`, which a reference type doesn't need. Add that rule to the instructions too.
* `SetBlendMode`: `Optional<SKBlendMode>`, `ValidateEnum`.

## Renderable

* Staged and committed fields: `_opacity`, `_imageFilter`, `_colorFilter`, `_blendMode`.
  * Override `BeginStyle` and `EndStyle` (calling base), resetting the staged fields to their defaults on each pass, so removing a setter falls back.
* `HasGroupEffect`: opacity below 1, a filter set, or a blend mode other than `SrcOver`.
* One lazily created `SKPaint _effectPaint` per node, synced only when the committed values change and disposed in `Dispose(bool)`.
* In `Render`, after `Concat`:
  * opacity 0: return (see decision 1);
  * `HasGroupEffect`: `canvas.SaveLayer(bounds?, _effectPaint)`, with the bounds policy from task 02;
  * then layer, clip and children as before;
  * the existing `RestoreToCount(save)` composites the group.
* Effects sit outside the clip, so a node's own shadow or blur isn't cut by its own Overflow; an ancestor's clip still cuts it.

## Decisions

1. **Opacity 0 and input.** Skip drawing in either case. Should the node still take pointer input?
   * Yes, as CSS does, so an invisible button still works.
   * No, it behaves like Display None for input only.
   * Recommended: yes, matching CSS. Display None is there to hide a node fully.
2. **Out-of-range opacity.** Ignore it with a warning, as the conventions do elsewhere, or clamp it, which is friendlier for animated values that overshoot? Recommended: ignore, for consistency.
3. **Scope of the first pass.** All four styles, or opacity and image filter first?

## Tests

* Styles: later wins; removing a setter returns to the default; a non-finite or out-of-range opacity is ignored; an unknown blend mode is ignored.
* Render (pixel checks):
  * group opacity over overlapping children is uniform;
  * opacity 0 draws nothing, including the subtree;
  * a grayscale color filter turns the subtree gray;
  * a blur spills or not as task 02 decided;
  * a drop shadow follows a scaled node;
  * a node without effects draws exactly as before.

## Deferred

* Opacity 0 input routing (with interaction).
* The `SkiaPaint` bridge (parked, `.vl` work).
