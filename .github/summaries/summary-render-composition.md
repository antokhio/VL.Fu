# Render Composition: VL & Skia Deep Dive Summary

> Companion investigation for [proposal-render-composition.md](../proposals/proposal-render-composition.md).
> Verified against the current `VL.Fu` codebase, `VL.StandardLibs` (`VL.Core`, `VL.Core.Skia`, `VL.Skia`), `YogaSharp`, and `SkiaSharp 2.88.8` IL/metadata.

---

## 1. Current Baseline in `VL.Fu` & `VL.Skia`

### 1.1 How `Renderable<TSelf>` and `RegionProvider` Work Today
* **Transform & Coordinate Space ([Renderable.cs](../../VL.Fu/src/Core/Rendering/Renderable.cs), [Layoutable.cs](../../VL.Fu/src/Core/Layout/Layoutable.cs)):**
  * `LocalTransformation` is `Translate(origin.X + Layout.X, origin.Y + Layout.Y) · Transformation`.
  * In `Render(CallerInfo caller)`:
    1. A root node (`Parent is null`) captures the caller's viewport into its caller space (`inv(caller.Transformation).MapRect(caller.ViewportBounds)`), used on the next `Update() -> CalculateRootLayout()` (1-frame viewport follow, unless explicit `SetBounds` is set).
    2. Early-exits if `!HasLayout || !IsDisplayed` (`_handle->GetDisplay() != YGDisplay.None`).
    3. Saves canvas state (`var save = canvas.Save()`), concatenates `LocalTransformation` onto `canvas`, and creates `inner = caller.WithTransformation(caller.Transformation.PreConcat(local))`.
    4. Renders own layer (`_layer?.Render(inner)`) in node-local coordinates `(0, 0, Layout.Width, Layout.Height)`.
    5. Renders `Children[i].Render(inner)` in tree order (`0 .. Count - 1`).
    6. Restores canvas via `canvas.RestoreToCount(save)` in `finally`.
* **How VL.Skia `Transform [Graphics.Skia.Transform]` Works (Verified from Patch Screenshot & [Transform.cs](../../../__MCP/VL.StandardLibs/VL.Core.Skia/Transform.cs)):**
  * **Pins:** `(ILayer Input, Matrix Transformation) -> ILayer Output` (with default `Matrix.Identity`: `M11=1, M22=1, M33=1, M44=1`).
  * **Patch implementation:** Converts Stride `Matrix` via `ToSKMatrix` and passes `(Input, SKMatrix)` into a stateful `TransformUpstream` instance (`Create` -> `Update`).
  * **C# `TransformUpstream` implementation ([Transform.cs](../../../__MCP/VL.StandardLibs/VL.Core.Skia/Transform.cs:48)):**
    * **`Render(CallerInfo caller)`:**
      ```csharp
      var us = caller.PushTransformation(M); // caller with { Transformation = caller.Transformation.PreConcat(M) }
      us.Canvas.SetMatrix(us.Transformation);
      try { Input?.Render(us); }
      finally { caller.Canvas.SetMatrix(caller.Transformation); }
      ```
    * **`Notify(INotification notification, CallerInfo caller)`:**
      ```csharp
      var us = caller.PushTransformation(M);
      us.Transformation.TryInvert(out var invTransformation);
      var newNotification = notification.WithSender(new IntermediateTransformNotificationSender(invTransformation, notification));
      return base.Notify(newNotification, us);
      ```
    * **`Bounds`:**
      ```csharp
      Input?.Bounds != null ? M.MapRect(Input.Bounds.Value.ToSKRect_()).ToRectangleF_() : null
      ```
  * **Why this matters for `Renderable<TSelf>`:**
    * `VL.Fu` already mirrors `Transform [Graphics.Skia.Transform]`'s exact facade (`Stride.Core.Mathematics.Matrix` converted via `ToSKMatrix()`), `PreConcat` composition order, and `Bounds` mapping (`LocalTransformation.MapRect(...)`).
    * Notice how `TransformUpstream.Render` does **not** use `canvas.Save()` / `canvas.Restore()`—it mutates the current save frame's matrix via `us.Canvas.SetMatrix(us.Transformation)` and restores `caller.Canvas.SetMatrix(caller.Transformation)` in `finally`. Because `Renderable<TSelf>.Render` keeps `canvas.TotalMatrix == inner.Transformation`, any `Transform [Graphics.Skia.Transform]` node placed inside `_layer` (or around a Fu root, or inside a composer) works seamlessly without losing the Fu node's matrix or clip!
    * And when we implement `Interactable<TSelf>.Notify`, `TransformUpstream.Notify` gives us the exact blueprint: push `LocalTransformation` onto `caller`, invert `us.Transformation` (or cache the inverse), and wrap `notification.WithSender(...)` using our own `IWorldSpace2d, IProjectionSpace` sender (since `IntermediateTransformNotificationSender` is `internal` to `VL.Core.Skia`).
* **How `RegionProvider` Hosts the Patch Inlay ([RegionProvider.cs](../../VL.Fu/src/Core/Region/RegionProvider.cs)):**
  * `RegionProvider` implements `IRegion<IRegionProvider.IRegionInlay>` where `IRegionInlay.Update(IFuNode node, out ILayer layer)` runs during the mainloop `Update()` fragment—**before** `Render(CallerInfo)` is called by the `SkiaRenderer`.

---

## 2. Clipping & Masking (`YGOverflow`, `ClipRect`, `ClipPath`, `Mask`)

### 2.1 How VL.Skia `ClipRect`, `ClipPath (Advanced)`, and `Mask` Work (Verified from VL.Skia Patches)

The screenshots of `ClipRect`, `ClipPath (Advanced)`, and `Mask` in `Graphics.Skia.Layers` reveal a **critical interaction between `SKCanvas` clipping and `CallerInfo.Transformation`** in `VL.Skia`:

1. **`ClipRect [Graphics.Skia.Layers]` (`VL.Skia.vl`):**
   * **Pins:** `(ILayer Input, RectangleF Bounds, bool Antialias, bool Apply) -> ILayer Output`.
   * **Internal implementation (via `SetRender`):**
     ```csharp
     var canvas = caller.Canvas;
     canvas.Save();
     var pixelRect = ToPixels(caller, Bounds); // maps Bounds through caller.Transformation
     canvas.ResetMatrix();
     canvas.ClipRect(pixelRect, antialias);
     canvas.SetMatrix(caller.Transformation);
     if (Input != null)
         Input.Render(caller);
     canvas.Restore();
     ```
   * **Why `ClipRect` in `VL.Skia` does `ToPixels` + `ResetMatrix` + `ClipRect` + `SetMatrix(caller.Transformation)`:**
     * In `VL.Skia`, drawing nodes (like `Rectangle`, `Circle`, `Text`) do **not** assume `canvas.TotalMatrix` equals `caller.Transformation`—many `VL.Skia` nodes leave `canvas.TotalMatrix` at identity (pixel space) and transform geometry/matrix via `caller.Transformation` when drawing, or call `canvas.SetMatrix(caller.Transformation)`!
     * **WAIT — Look closely at what happens if `canvas.TotalMatrix` is at Identity inside `VL.Skia` drawing nodes:**
       * `SkiaRenderer` initializes `CallerInfo` with `Identity`, and `WithinCommonSpace` (`SetSpaceUpstream2`) calls `us.Canvas.SetMatrix(us.Transformation)`!
       * And in `VL.Fu`, `Renderable<TSelf>.Render` does `canvas.Concat(ref local)` so `canvas.TotalMatrix == inner.Transformation`!
       * However, why did `VL.Skia`'s `ClipRect` and `ClipPath` do `ToPixels(caller, Bounds)` + `ResetMatrix()`? Because some `VL.Skia` nodes only update `CallerInfo.Transformation` (via `TweakCallerInfo`) without calling `canvas.SetMatrix` until the leaf drawing node!
       * **Even more importantly:** in `VL.Skia`'s `ClipRect`, `ToPixels` maps a `RectangleF` through `caller.Transformation` (`caller.Transformation.MapRect(rect)`), which turns a **rotated** rectangle into its **axis-aligned bounding box in pixel space** before calling `ResetMatrix()` + `ClipRect`!
       * By contrast, in `VL.Fu`, because `Renderable<TSelf>.Render` already calls `canvas.Concat(ref local)` (so `canvas.TotalMatrix == inner.Transformation`), calling `canvas.ClipRect(localRect, SKClipOperation.Intersect, antialias)` directly on `canvas` (without `ResetMatrix()`) clips in **true node-local space (supporting rotated clips when `antialias: true`)** AND stays in pixel-sync with `inner.Transformation`!
   * **Note on `ClipRect.Bounds`:** Passes ` Nullable.Create(Bounds)` to `SetRender.bounds` so the clipped layer reports `Bounds` as its `ILayer.Bounds`.

2. **`ClipPath (Advanced) [Graphics.Skia.Layers]` (`VL.Skia.vl`):**
   * **Pins:** `(ILayer Input, SKClipOperation Operation, SKPath Path, bool Antialias, bool Apply) -> ILayer Output`.
   * **Internal implementation (via `SetRender`):**
     * Maps `Path` through `ToPixels(caller, Path)` (`path.Transform(caller.Transformation)`), then inside `SetRender`: `canvas.Save()` -> `canvas.ResetMatrix()` -> `canvas.ClipPath(pixelPath, Operation, Antialias)` -> `canvas.SetMatrix(caller.Transformation)` -> `Input?.Render(caller)` -> `canvas.Restore()`.
   * **Note on `ClipPath.Bounds`:** Passes `Input.Bounds` through unchanged to `SetRender.bounds`.

3. **`Mask [Graphics.Skia.Layers]` (`VL.Skia.vl`):**
   * **Pins:** `(ILayer Input, ILayer Mask, bool UseLuma, bool ShowHelpers, bool ShowMaskOnly) -> ILayer Output`.
   * **Internal implementation (built entirely out of `Precompose` + `Group`!):**
     * **Normal mode (`!ShowMaskOnly`):**
       * Wraps `Mask` in `Precompose` with `Paint = SetBlendMode(SKBlendMode.DstIn)` (and if `UseLuma` is true, chains `LumaColor` -> `SetColorFilter` onto that same `Paint`).
       * Optionally, if `ShowHelpers` is true, adds a second `Precompose(Mask, SetColorFilter(LumaColor -> AlphaToRed(0.50)))` helper overlay.
       * Combines `Group(Input, PrecomposedMask, [HelperOverlay])` and wraps the whole `Group` in an **outer `Precompose` (with default `Paint`)**!
     * **How Skia executes `Mask`:**
       1. Outer `Precompose` opens an isolated offscreen `SaveLayer(null)` so `DstIn` only affects `Input`, not the background behind the node.
       2. `Input` renders normally into that offscreen buffer.
       3. Inner `Precompose(Mask, DstIn [+ LumaColor])` opens a second `SaveLayer` with `BlendMode = SKBlendMode.DstIn` (and optional `SKColorFilter.CreateLumaColor()`), renders `Mask` (`ILayer`), and on `Restore()` multiplies the destination (`Input`) alpha by the `Mask` layer's alpha (or luma)!
       4. Outer `Precompose` restores and composites the masked result onto the main canvas.
     * **`ShowMaskOnly` mode:**
       * Wraps `Mask` in `Precompose(Mask, SetColorFilter(GetAlpha))` to visualize the mask's alpha channel.

---

### 2.2 Available Ways to Fulfill Clipping & Masking in `VL.Fu`

In addition to automatic Yoga `Overflow` clipping, `VL.Fu` can support explicit `ClipRect`, `ClipPath` (or rounded-rect), and `Mask` (`ILayer` mask) over any node + its subtree:

#### A. Automatic Layout Overflow Clipping (`YGOverflow.Hidden` / `YGOverflow.Scroll`)
* **Reading Overflow & Border from Yoga ([YGNode.cs](../../../__MCP/YogaSharp/YogaSharp/YGNode.cs)):**
  * `_handle->GetOverflow()` returns `YGOverflow.Visible` (default), `YGOverflow.Hidden`, or `YGOverflow.Scroll`.
  * `_handle->GetComputedBorder(YGEdge.Left / Top / Right / Bottom)` returns the resolved physical border widths after `CalculateLayout`.

| Option | Clip Rectangle in Node Space | Placement in `Render` | Trade-offs |
| :--- | :--- | :--- | :--- |
| **C1: Padding-Box Clip, Children Only (Recommended / CSS-like)** | `SKRect(borderLeft, borderTop, width - borderRight, height - borderBottom)` (clamped so `Right >= Left`, `Bottom >= Top`) | **After** `_layer?.Render(inner)`, **before** `children[i].Render(inner)` | Matches CSS: the node's own `Layer` (background, border stroke, outer glow/drop-shadow drawn on the node itself) is **not** clipped by its own `Overflow`, and children do not draw over the node's border. |
| **C2: Full Layout-Box Clip, Children Only** | `SKRect(0, 0, Layout.Width, Layout.Height)` | **After** `_layer?.Render(inner)`, **before** `children[i].Render(inner)` | Simpler (ignores `SetBorder`), but if the user sets `SetBorder` and draws a border in `Layer`, children only clip at the outer edge and overlap the border. |
| **C3: Clip Own Layer + Children** | `SKRect(0, 0, Layout.Width, Layout.Height)` | **Before** `_layer?.Render(inner)` | Clips the node's own `Layer` as well (useful if `Layer` draws an oversized image, but breaks outer strokes/shadows on `_layer` and differs from CSS). |

#### B. Additional Custom Clip (`ClipRect`, `ClipRoundRect`, `ClipPath`) & `Mask` (`ILayer`) on a Fu Subtree
Because `ClipRect`, `ClipPath`, and `Mask` are core UI composition tools (e.g., clipping a card + its children to a rounded rectangle or custom `SKPath`, or masking a container + its children with a gradient/shape `ILayer`), there are **two ways** they can be used in `VL.Fu`:

1. **As Fu-Native Styles (`Style<IFuNode>` staged on `Renderable<TSelf>`) — Recommended for `ClipRect` / `ClipRoundRect` / `ClipPath` / `Mask`:**
   * **`SetClipRect(Optional<RectangleF> rect, SKClipOperation operation = Intersect, bool antialias = true)`:**
     * Stages an explicit clip rect in node-local space (or `SetCornerRadius` / `SetClipRoundRect` which uses the node's `(0, 0, Layout.Width, Layout.Height)` + corner radii automatically!).
     * Applied in `Renderable.Render` via `canvas.ClipRect` / `canvas.ClipRoundRect` **before** `_layer?.Render(inner)` and `Children` (so a rounded-corner clip or custom clip path clips both the node's background layer AND its children cleanly!).
   * **`SetClipPath(Optional<SKPath> path, SKClipOperation operation = Intersect, bool antialias = true)`:**
     * Stages an `SKPath` in node-local space and calls `canvas.ClipPath(path, operation, antialias)` before `_layer` and `Children`.
     * Unlike `VL.Skia`'s `ClipRect` (which did `ToPixels` + `ResetMatrix` and lost rotation on `RectangleF`), calling `canvas.ClipRect` / `canvas.ClipRoundRect` / `canvas.ClipPath` directly while `canvas.TotalMatrix == inner.Transformation` keeps the clip rotated/scaled in exact node-local space!
   * **`SetMask(Optional<ILayer> mask, bool useLuma = false)` (as a Style or Pin on `Renderable<TSelf>`):**
     * In `Renderable.Render`, when `_maskLayer is not null`:
       1. Open outer `canvas.SaveLayer(_effectPaint)` (can even combine with the E1 group effect `SaveLayer` if no separate blend/filter conflict, or nest cleanly!).
       2. Draw `_layer` + clip + `Children`.
       3. Open inner `canvas.SaveLayer(_maskPaint)` where `_maskPaint` is preconfigured with `BlendMode = SKBlendMode.DstIn` (and cached `SKColorFilter.CreateLumaColor()` when `useLuma` is true).
       4. Call `_maskLayer.Render(inner)` in the node's local space `(0, 0, Layout.Width, Layout.Height)`.
       5. `canvas.Restore()` (applies `DstIn` mask) and `canvas.RestoreToCount(save)`!
     * This reproduces the exact `VL.Skia` `Mask` node behavior natively in C# over the whole Fu subtree without needing 4 nested `Precompose`/`Group` nodes!

2. **Via `node.ChildrenLayer` Inside `FuNode` (Way E2-B):**
   * If a user prefers patching with the existing `VL.Skia` `ClipRect`, `ClipPath (Advanced)`, or `Mask` nodes directly inside the `FuNode` region (including `Mask`'s `ShowHelpers` / `ShowMaskOnly` debug pins), exposing `node.ChildrenLayer` (`ILayer`) lets them wire `Group(ownLayer, node.ChildrenLayer) -> Mask / ClipPath / ClipRect -> layer` output right inside `FuNode`!

---

## 3. Group Effects & Hosting VL.Skia `Precompose` (§ 4 of Proposal)

### 3.1 How VL.Skia `Precompose` & `SkiaPaint` Actually Work (Verified from VL.Skia Patches)

The screenshots from `VL.Skia.vl` and [SkiaPaintExtensions.cs](../../../VL.Fu5/VL.Fu/src/Paint/SkiaPaintExtensions.cs) reveal the exact architecture of `Precompose` and `SkiaPaint` in `VL.Skia`:

1. **`Precompose` (`Graphics.Skia.Advanced.Layers` in `VL.Skia.vl`):**
   * **Pins:**
     * Inputs: `Input` (`ILayer`), `Paint` (**`SkiaPaint`** — *not* `SkiaSharp.SKPaint`!)
     * Output: `Output` (`ILayer`)
   * **Internal implementation:**
     * `Precompose` is simply a 3-way `Group` of:
       1. `SaveLayer(Paint)` (an `ILayer` whose `Render` calls `SKPaint(out var skPaint)` on the `SkiaPaint` record and calls `caller.Canvas.SaveLayer(skPaint)`)
       2. `Input` (`ILayer`)
       3. `RestoreLayer` (an `ILayer` whose `Render` calls `caller.Canvas.Restore()`)
     * Note the comment inside `Precompose`: `ABOUT SAVE/RESTORE LAYER: https://groups.google.com/d/msg/skia-discuss/m5Ch1i4odAo/gsdh9kwADQAJ`.
     * It does **not** record an `SKPicture` or allocate an `SKSurface` of its own—it literally brackets `Input.Render(caller)` between `canvas.SaveLayer(paint)` and `canvas.Restore()`, keeping the canvas transform and routing `Notify` directly through `Group` to `Input`!

2. **CRITICAL VL.Skia Fact: The Paint Pin Type in VL Patches is `SkiaPaint` (a VL Record), NOT `SkiaSharp.SKPaint`!**
   * In `VL.Skia.vl`, every paint modifier node (`Fill`, `Stroke`, `SetAlpha`, `SetColor`, `SetBlendMode`, `SetImageFilter`, `SetColorFilter`, `SetShader`, `SetMaskFilter`, etc. in `Graphics.Skia.Paint`) operates on **`Graphics.Skia.SkiaPaint`**, a **patch-defined VL Record** inside `VL.Skia.vl`!
   * **How `SkiaPaint` works internally (from `SkiaPaint` & `TweakLessFrequentlyUsed` screenshots):**
     * `SkiaPaint` is a VL Record holding sub-records:
       * `Color` (`Color4`), `Shader` (`SKShader`), `Favorites` (`SkiaPaintFavorites`), `LessFrequent` (`SkiaPaintLessFrequentlyUsed` — holds `ImageFilter`, `ColorFilter`, `MaskFilter`, `BlendMode`, etc.), `FinePrint` (`SkiaPaintFinePrint`), plus a shared `Paint` (`SKPaint`) and `PaintSyncer`.
     * As the comment inside `SkiaPaint` states: **"all snapshots of this record share one long living SKPaint and one syncer"**.
     * Nodes like `SetImageFilter` take `Input: SkiaPaint` and `Filter: SKImageFilter`, use a `Cache` region inside `TweakLessFrequentlyUsed`, and output a new `SkiaPaint` record snapshot via `WithInputAndLessFrequent`.
     * When a drawing node (or `SaveLayer` inside `Precompose`) needs the actual `SkiaSharp.SKPaint` at render time, it calls the internal operation **`SkiaPaint.SKPaint(out SKPaint Paint)`**, which runs `PaintSyncer.Update(Paint, Color, Shader, Favorites, LessFrequent, FinePrint)` to sync the record's fields onto the shared `SKPaint` instance and returns it!
   * **Why C# code in `VL.Fu` cannot reference `SkiaPaint` at compile time:**
     * Because `SkiaPaint` is defined in `VL.Skia.vl` (compiled dynamically by vvvv at runtime, not in `VL.Core.Skia.dll` or `VL.Skia.dll`), C# code in `VL.Fu.csproj` cannot reference the `SkiaPaint` type directly!
     * **How [SkiaPaintExtensions.cs](../../../VL.Fu5/VL.Fu/src/Paint/SkiaPaintExtensions.cs) solved this in `VL.Fu5`:**
       * It accepted `object? input` and used reflection (`type.GetMethod("SKPaint", ...)` with `out SKPaint`, falling back to the `SKPaint` field) to extract the `SKPaint`!
       * **Even better warn-free / zero-reflection-overhead options for `VL.Fu`:**
         1. **Cached Open Delegate (`Delegate.CreateDelegate`)** in C#: on first encounter of the runtime `SkiaPaint` type, bind a strongly-typed ` delegate void GetSKPaintDelegate(object instance, out SKPaint paint)` (or `MethodInfo.CreateDelegate`) once per process so calling `SKPaint(out var paint)` Syncer costs a single delegate call with **zero boxing or `object[]` allocation per frame**! (Crucial: you MUST call the `SKPaint(out SKPaint)` method rather than reading the raw `Paint` field, because `SKPaint(out SKPaint)` is what triggers `PaintSyncer` to apply the snapshot's `Color`/`LessFrequent`/`ImageFilter` values to the shared `SKPaint`!).
         2. **Or a tiny bridge in `VL.Fu.vl`:** Since `VL.Fu.vl` references `VL.Skia.vl`, a thin forward/wrapper in `VL.Fu.vl` can accept `SkiaPaint` with full static typing in VL and pass either `SKPaint` or the Individual `SKImageFilter` / `SKColorFilter` / `SKBlendMode` / `Alpha` into C#!
         3. **Filter objects themselves (`SKImageFilter`, `SKColorFilter`, `SKShader`, `SKMaskFilter`, `SKBlendMode`) ARE real `SkiaSharp` C# types!** In `VL.Skia`, nodes like `Blur`, `DropShadow`, `ColorMatrix`, `Lighting` output `SkiaSharp.SKImageFilter` and `SkiaSharp.SKColorFilter` directly (as seen in the `SetImageFilter` screenshot, where the `Filter` pin goes into `SkiaPaintLessFrequentlyUsed.SetImageFilter` and is a `SkiaSharp.SKImageFilter`).

3. **Important Lesson from `FuPrecompose` in `VL.Fu5` ([FuPrecompose.cs](../../../VL.Fu5/VL.Fu/src/FuPrecompose.cs)):**
   * In `VL.Fu5`, `FuPrecompose` tried to be a standalone `ILayer` node `Update(IFuNode input, float alpha, out IFuNode output)` that returned `output = input`.
   * Because `output` was still `input` (`IFuNode`), when `input` was connected as a child of a parent `FuNode`, the parent `FuNode` called `child.Render(caller)` on `input` directly—**bypassing `FuPrecompose.Render` completely**! (And if `FuPrecompose` were rendered directly, it called `_input.Render` AND looped over `_input.Children` again, double-rendering the children!).
   * In `VL.Fu`, `Renderable<TSelf>.Render` is the single place that renders a node and its children in the tree, which is why group effects (`SaveLayer`) must either be **on `Renderable<TSelf>` itself** (via `Style<IFuNode>` or a pin on `FuNode`) or wired through `FuNode`'s region inlay!

---

### 3.2 Available Ways to Fulfill Group Effects & Precompose Hosting

With `Precompose` and `SkiaPaint` now completely clear, we can compare all concrete ways to fulfill group effects:

#### Way E1-A: Fu-Native Effect Styles Taking Typed Skia Objects (`SetOpacity`, `SetImageFilter`, `SetColorFilter`, `SetBlendMode`)
Add non-Yoga style records (`OpacityStyle`, `ImageFilterStyle`, `ColorFilterStyle`, `BlendModeStyle`) deriving from `Style<IFuNode>`, staged on `Renderable<TSelf>` (just like `StageTransformation` on `Layoutable<TSelf>`).

* **How it works:**
  * In `VL.Skia`, filter producer nodes (`Blur`, `DropShadow`, `ColorMatrix`, `Lighting`, etc.) output real C# `SkiaSharp.SKImageFilter` and `SkiaSharp.SKColorFilter` instances (as confirmed by the `SetImageFilter` screenshot).
  * `Renderable<TSelf>` holds staged and committed effect fields (`_opacity = 1f`, `_imageFilter = null`, `_colorFilter = null`, `_blendMode = SKBlendMode.SrcOver`) and a single lazily-created, reusable `SKPaint? _effectPaint` (disposed in `Dispose(bool)`).
  * During `Render(caller)`:
    * If `_opacity <= 0f`: skip drawing `_layer` and `Children` completely (0 draw calls, 0 offscreen allocation).
    * If `HasGroupEffect` (`_opacity < 1f || _imageFilter is not null || _colorFilter is not null || _blendMode != SKBlendMode.SrcOver`):
      * Sync `_effectPaint` (`Color = new SKColor(255, 255, 255, (byte)Math.Clamp(MathF.Round(_opacity * 255f), 0f, 255f))`, `ImageFilter = _imageFilter`, `ColorFilter = _colorFilter`, `BlendMode = _blendMode`).
      * Call `canvas.SaveLayer(_effectPaint)` after `canvas.Concat(ref local)`, draw `_layer` + clip + `Children`, and `canvas.RestoreToCount(save)` composites the entire subtree in one pass!
* **Pros:**
  * Idiomatic Fu UX: works in `Combine`, style chains, and shared styles across multiple nodes.
  * Zero reflection, 100% strongly typed in C#.
  * Zero overhead when unused; reuses a single `SKPaint` per node when active.
  * Because `Precompose` in `VL.Skia` is literally just `SaveLayer(paint)` + `Input` + `RestoreLayer`, **building `SaveLayer(_effectPaint)` directly into `Renderable<TSelf>.Render` does the exact same thing `Precompose` does**, without needing `Group` + `SaveLayer` + `RestoreLayer` wrappers!

---

#### Way E1-B: Accepting a Whole `SkiaPaint` Chain (`SetGroupPaint` / `SetPaint` Style or Pin)
What if a user wants to build a paint chain using existing `VL.Skia` paint nodes (`SetAlpha` -> `SetImageFilter` -> `SetColorFilter` -> `SetBlendMode`) and pass that `SkiaPaint` directly into Fu (just like they pass `Paint` into `Precompose`)?

* **How it works:**
  * Because `SkiaPaint` is a VL record where **"all snapshots of this record share one long living SKPaint and one syncer"**, and calling `SkiaPaint.SKPaint(out SKPaint paint)` mutates that shared `SKPaint` in place:
    1. **At `Render` time (e.g., a `Paint` pin on `FuNode` or a `SkiaPaintStyle` evaluated during `Render`):** Calling `SKPaint(out var skPaint)` via a cached open delegate immediately before `canvas.SaveLayer(skPaint)` syncs the snapshot onto `SkiaPaint`'s shared `SKPaint` and passes it straight to `SaveLayer(skPaint)`.
    2. **At `SetStyle` time (in `EndStyle()`):** If `SkiaPaintStyle` extracts the values during `Build(IFuNode)` by calling `SKPaint(out var tempPaint)` and **copying** `tempPaint.Color.Alpha`, `tempPaint.ImageFilter`, `tempPaint.ColorFilter`, and `tempPaint.BlendMode` into `Renderable`'s staged fields (`StageOpacity`, `StageImageFilter`, `StageColorFilter`, `StageBlendMode`), then:
       * Sharing one `SkiaPaint` across multiple branches won't clobber each node's `SKPaint` at render time!
       * `SkiaPaint` and `SetOpacity` / `SetImageFilter` compose in the exact same style pipeline!
  * **Wait—Does `SkiaPaint` change reference when upstream `SetImageFilter` changes?**
    * **YES!** Look at screenshot 3 (`TweakLessFrequentlyUsed`): `WithInputAndLessFrequent` is a VL Record mutation on `SkiaPaint` (`State`), which outputs a **new `SkiaPaint` record snapshot instance** whenever the `Cache` region re-executes! So `ReferenceEquals` / `EqualityComparer<object>.Default` in `StyleNode` detects changes automatically!
  * **Why C# reflection is only needed once if exposed from C#:**
    * Since `SkiaPaint` is a `.vl` type, a C# pin typed as `object` shows up as `Object` in VL unless typed in `VL.Fu.vl` or constrained. However, if we expose `SetOpacity`, `SetImageFilter`, `SetColorFilter`, `SetBlendMode` directly in C# (Way E1-A), pins have exact types (`float`, `SKImageFilter`, `SKColorFilter`, `SKBlendMode`) with zero reflection needed!

---

#### Way E2-A: Composer Pin via `Func<ILayer, ILayer>` (`SetComposer` Fragment)
Expose a pin on `Renderable<TSelf>` (or `FuNode`):
```csharp
[Fragment(Order = PinOrder.Behavior)] // or dedicated order
public void SetComposer([Pin(Visibility = PinVisibility.Optional)] Func<ILayer, ILayer>? composer)
```
where `Renderable<TSelf>` holds a lightweight `SubtreeLayer : ILayer` wrapper whose `Render(c)` draws `_layer` + clip + `Children` and whose `Notify(n, c)` routes notifications to `Children` + `_layer`.

* **Why do we even need `Precompose` if `Precompose` is just `SaveLayer` + `RestoreLayer`?**
  * Looking at Screenshot 1 (`Precompose`), `Precompose` does **nothing** that `canvas.SaveLayer(paint)` in `Renderable.Render` (Way E1) doesn't already do!
  * The *only* case where a user needs to wrap a Fu subtree with arbitrary `ILayer` nodes is if they want a VL.Skia layer effect that is **not** a paint effect (for example, `Mask`, `WithinCommonSpace`, custom shader layer, or `OffScreenRenderer`).
* **VL Lifecycle Caveat for `Func<ILayer, ILayer>`:**
  * In VL, a `Func<ILayer, ILayer>` delegate region is stateless (you cannot place stateful ProcessNodes like `Precompose` or `Blur` inside a plain `Func` delegate region without managing state outside).

---

#### Way E2-B: In-Region Subtree Layer via `IFuNode` Inside `FuNode`'s Existing Region (Zero New Regions!)
Look at `IRegionProvider.IRegionInlay` in [RegionProvider.cs](../../VL.Fu/src/Core/Region/RegionProvider.cs:16):
```csharp
public interface IRegionInlay
{
    void Update(IFuNode node, out ILayer layer);
}
```
The `FuNode` region **already** passes `IFuNode node` into the user's stateful region patch on `Update()` and receives `out ILayer layer` back!

* **How it works:**
  * Expose a property `ILayer ChildrenLayer { get; }` on `IFuNode` / `Renderable<TSelf>` (an `ILayer` that renders/notifies the clipped children of this node in local space).
  * Inside `FuNode`'s region patch:
    * **Normal case:** User ignores `node.ChildrenLayer` and just outputs their own background `ILayer`. `Renderable.Render` draws `_layer` and then `Children`.
    * **Custom composition case (`Precompose` / `Mask` / custom `ILayer` wrap):** Inside the `FuNode` region, the user groups their own background layer with `node.ChildrenLayer`, passes it into `Precompose` (with `SetImageFilter` -> `Blur` right there inside the stateful `FuNode` region!), and connects `Precompose.Output` to the region's `layer` output!
    * When `ChildrenLayer.Render` runs during `_layer.Render(inner)`, it sets a pass flag so `Renderable.Render` knows the region inlay already rendered the children as part of `_layer` and doesn't draw them a second time.
* **Why Way E2-B beats E2-A (`Func<ILayer, ILayer>`) and E3 (feedback loop):**
  1. `FuNode` is **already a stateful region** (`IRegionInlay`) updated in `Update()` and disposed in `Dispose()`!
  2. Users can drop `Precompose`, `SetImageFilter`, `Blur`, `Group`, etc. directly inside `FuNode` where they already draw the node's visual content—no second region, no stateless `Func` limitation, and no feedback loop!

---

#### Way E3: Content Output + Composite Input (Feedback Loop on One ProcessNode)
* **VL Compiler Behavior:** In VL, wiring an output pin of a ProcessNode back into an input pin of the **same** ProcessNode instance in the same patch creates a cyclic dependency between fragments unless broken by a `FrameDelay` or explicit fragment scheduling (`FragmentOrder`).
* If broken by a frame delay, rendering and hit-testing lag by 1 frame and `CallerInfo` during `Render` is out of phase with `Update`.
* **Verdict:** Avoid E3; Way E1 + Way E2-B (or E2-A updated in `Update()`) completely supersede it without feedback loops.

---

#### Way E4: `CallerInfo.WithRenderPaintHack` (`HackRendering`)
* **VL.Skia Implementation ([Layers.cs](../../../__MCP/VL.StandardLibs/VL.Skia/src/Layers.cs:81)):**
  * `caller.WithRenderPaintHack(Func<object, object>)` sets `CallerInfo.RenderInfoHack`.
  * Upstream VL.Skia primitive nodes invoke `RenderInfoHack(paint)` before drawing.
* **Limitations:**
  1. Applies **per-primitive**, not as a group (overlapping shapes bleed through each other under alpha < 1, blurs apply per-shape).
  2. Custom `ILayer` implementations that draw directly to `caller.Canvas` without calling `RenderInfoHack` ignore it completely.
* **Verdict:** Not suitable for group effects; only relevant if Fu ever wants a "tint/hack upstream strokes" utility.

---

## 4. Z-Index & Stacking / Overlay (§ 5 of Proposal)

### 4.1 Option Z1: Sibling-Scoped Z-Index (Local Stacking Context per Node)
* **How it works:**
  * Add `ZIndexStyle(IReadOnlyList<IStyle> Inputs, int ZIndex) : Style<IFuNode>(Inputs)` and `SetZIndexNode` (`Optional<int>`).
  * Stage `_styledZIndex = 0` in `BeginStyle()`, commit to `_zIndex` in `EndStyle()`.
  * When a node's `_zIndex` changes, notify `Parent` to mark its `_sortedChildrenDirty = true` (also dirtied in `OnChildrenChanged`).
  * **Fast Path:** Track whether all children have `ZIndex == 0` (`_hasNonZeroChildZIndex`). When false (the 99% case), iterate `Children` directly with **zero extra array/list allocation**.
  * **Sorted Path:** When any child has `ZIndex != 0`, sort a cached `TSelf[]` or `List<TSelf>` using a **stable sort** (`Array.Sort` with index tie-breaker or `OrderBy(c => c.ZIndex)`) so equal `ZIndex` values preserve tree order:
    * `Render` iterates forward (`0 .. Count - 1`: lowest `ZIndex` first, highest `ZIndex` painted last/on top).
    * `Notify` iterates in reverse (`Count - 1 .. 0`: highest `ZIndex` receives pointer events first).
* **Yoga Interaction:**
  * Yoga layout order (`_handle->SetChildren(handles)`) MUST stay in **logical `Children` tree order**, NOT `ZIndex` order! Otherwise changing `ZIndex` would reorder flex items in a row/column. Keeping `ZIndex` purely in `Renderable` / `Interactable` leaves flex layout order untouched.

### 4.2 Option Z3: Overlay / Portal (Root-Level Deferred Pass)
* **Why Z1 alone isn't enough for Dropdowns / Tooltips:**
  * Under Z1 (and because ancestors may have `Overflow.Hidden` or `SaveLayer` group effects), a child inside a clipped scroll view or a card with a lower sibling order cannot paint above an ancestor's next sibling or escape an ancestor's `ClipRect`.
* **How Z3 works cleanly on top of Z1:**
  * A root node maintains an overlay queue/list during `Render` (and `Notify`).
  * A node marked as an overlay (or a `Portal` / `Popup` node) records its world-to-root `SKMatrix` (`caller.Transformation` relative to root) during the tree walk instead of drawing inside its parent's clip, and the root renders all overlays **after** the main tree walk (and notifies them **before** the main tree walk).
  * Can be added cleanly in the interaction/popup phase without changing Z1.

---

## 5. Deep Technical Findings & Edge Cases to Watch Out For

1. **`SaveLayer` Bounds vs `SKImageFilter` Clipping (Proposal § 4.3 & Experiment 3):**
   * Passing `canvas.SaveLayer(nodeBox, paint)` when `paint.ImageFilter` is a blur or drop-shadow **clips the blur/shadow to `nodeBox`** in Skia 2.88!
   * **Solution:**
     * If `_imageFilter is not null`: either call `canvas.SaveLayer(paint)` (null bounds—bounded by the current canvas clip) or provide an optional `EffectBounds` / outset.
     * If `_imageFilter is null` (only Opacity, ColorFilter, or BlendMode): if the node is clipped (`Overflow != Visible`), pass `_clipRect`; otherwise pass `SaveLayer(paint)` or cached subtree bounds.
2. **Coordinate Space of `SKImageFilter` (Blur Sigma & DropShadow Offset):**
   * In Skia, when `SaveLayer` is called **after** `canvas.Concat(ref local)`, `SKImageFilter` parameters (`sigmaX`, `sigmaY`, `dx`, `dy`) are interpreted in the **current local coordinate space** (and scaled by the CTM into device pixels!).
   * This is a major advantage for Fu: in `DIPTopLeft` (where 1 unit = 100 DIP) or pixel space, `Blur` / `DropShadow` sigmas and offsets scale naturally with the node's space and `Transformation`!
3. **`Renderable.Bounds` When Clipped or Transformed:**
   * Today `Renderable.Bounds` reports `_explicitBounds ?? _computedBounds` where `_computedBounds` is `(0, 0, Layout.Width, Layout.Height)` mapped by `LocalTransformation`.
   * Even if children overflow a `Visible` parent, `Bounds` is the node's own box; if `SubtreeBounds` is added later, `IsClipped` clamps the subtree bounds to the node's clip rect.

---

## 6. Answers to § 8 & § 9 of [proposal-render-composition.md](../proposals/proposal-render-composition.md)

The screenshots of `Precompose`, `SetImageFilter`, `TweakLessFrequentlyUsed`, and `SkiaPaint` directly answer Question 7 and Experiment 7 from the proposal:

* **How `Precompose` works (Experiment 7 & Question 7):**
  * `Precompose` (`Graphics.Skia.Advanced.Layers`) takes `(ILayer Input, SkiaPaint Paint) -> ILayer Output`.
  * It does **not** record an `SKPicture` or render to a separate `SKSurface`. It is a 3-input `Group(SaveLayer(Paint), Input, RestoreLayer)`.
  * Because `SaveLayer` only pushes an offscreen layer on the current `SKCanvas` without resetting `TotalMatrix` or `CallerInfo.Transformation`, **it preserves the canvas transform and routes `Notify` unchanged**.
  * Also note: `Precompose` in `VL.Skia` uses the **unbounded** `SaveLayer(paint)` (no `SKRect` limit pin), which means Skia uses the current canvas clip as the offscreen buffer bounds and never clips blur/drop-shadow radii at the element box edge unless a clip is active!
* **Why `E1` (`SaveLayer` in `Renderable.Render`) + `E2-B` (`ChildrenLayer` on `IFuNode`) is the cleanest combination:**
  1. Since `Precompose` in `VL.Skia` is literally just `SaveLayer(paint)` + `Input` + `RestoreLayer`, **Way E1** (`SetOpacity`, `SetImageFilter`, `SetColorFilter`, `SetBlendMode` in `VL.Fu`) performs the **exact same `SaveLayer` operation natively inside `Renderable.Render`**, without the user having to patch `Precompose` + `SkiaPaint` at all!
  2. And if a user *does* want to use `Precompose` (or `Mask`, or any other `VL.Skia` `ILayer` node) over a node and its children, **Way E2-B** (`node.ChildrenLayer` inside `FuNode`'s existing region) lets them wire `node.ChildrenLayer` directly into `Precompose` inside `FuNode` with zero new regions and full process-node state persistence!

---

## 7. Summary of Recommended Path for [proposal-render-composition.md](../proposals/proposal-render-composition.md)

| Area | Recommended Approach | Why |
| :--- | :--- | :--- |
| **1. Layout Overflow Clipping** | **C1: Padding-Box Clip (`Layout` inset by `GetComputedBorder`) after `_layer?.Render`, before `Children`** | Matches CSS; lets `_layer` draw borders/backgrounds unclipped while clipping children inside the border. Read directly from `_handle->GetOverflow()`. |
| **2. Custom Clip & Mask (`ClipRect`, `ClipPath`, `Mask`)** | **Fu-native `Style<IFuNode>` styles** (`SetClipRect`, `SetClipPath`, `SetMask`) staged on `Renderable<TSelf>` (applied around `_layer` + `Children`) | Calling `canvas.ClipRect` / `canvas.ClipPath` while `canvas.TotalMatrix == inner.Transformation` preserves true rotated node-space clipping (unlike `VL.Skia.ClipRect`'s `ToPixels` + `ResetMatrix` which axis-aligns `RectangleF`). `SetMask` executes the `SaveLayer` + `DstIn` (+ optional `LumaColor`) pattern over the whole subtree. |
| **3. Group Effects** | **E1: Fu-native `Style<IFuNode>` styles** (`SetOpacity`, `SetImageFilter`, `SetColorFilter`, `SetBlendMode`) staged on `Renderable<TSelf>` using `canvas.SaveLayer` | Zero cost when unused; composes in `Combine`; reuses VL.Skia's existing `SKImageFilter` and `SKColorFilter` nodes directly on style pins! |
| **4. Hosting `Precompose` / Custom `ILayer` Wrappers** | **E2-B: Expose `ChildrenLayer` on `IFuNode` (usable right inside `FuNode`'s existing stateful region)** (or `SetComposer` evaluated in `Update`) | `FuNode` *already* has a stateful `IRegionInlay.Update(IFuNode node, out ILayer layer)`. Exposing `node.ChildrenLayer` lets the user wrap `Layer` + `ChildrenLayer` with `Precompose`, `Mask`, `ClipPath`, or any stateful `VL.Skia` node inside the region with zero extra regions, zero feedback loops, and full state persistence. |
| **5. Z-Index** | **Z1: Sibling-scoped `SetZIndex` (`Style<IFuNode>`)** with zero-allocation fast path when all children are `0`; **Z3 (Overlay/Portal)** deferred to interaction/popup phase | Keeps Yoga flex order intact (`_handle->SetChildren` stays in tree order); only `Render` (forward) and `Notify` (reverse) use the cached stable-sorted child list. |
