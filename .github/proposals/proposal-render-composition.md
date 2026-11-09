# Proposal: Render Composition (clipping, group effects, Precompose hosting, z-index)

> Code samples are guidance for the shape of the implementation, not code to copy.
> This is an investigation proposal: §§ 3–6 lay out options, § 8 lists experiments to run before deciding, § 9 lists the questions to answer first.

## 1. Context

`Renderable<TSelf>.Render` today:

```text
Render(caller)
  root: capture viewport
  skip if not laid out or display none (with subtree)
  save
  concat LocalTransformation          (canvas and CallerInfo.Transformation in sync)
  Layer?.Render(inner)                (the node's own VL.Skia layer, set with SetLayer)
  children[i].Render(inner)           (tree order = paint order)
  restore
```

`Notify` is still a stub. `Overflow` is a Yoga style now (`SetOverflow`), but only affects layout.

What we want next:

* **Clipping:** `Overflow` Hidden/Scroll clips the children to the node.
* **Group effects:** fade, blur, color-filter or blend a node **with everything it draws**: its own layer and its whole subtree ("the whole upstream" of a Fu node), as one image, not primitive by primitive.
* **Hosting VL.Skia Precompose** and similar VL.Skia layer nodes on a Fu subtree, so users can reuse the Skia toolbox instead of Fu re-implementing every effect.
* **Z-index:** paint (and hit test) order that differs from tree order, e.g. a raised card, a dropdown above its siblings.

## 2. Skia and VL.Skia facts this relies on

Checked against SkiaSharp **2.88.8** (what `VL.Core.Skia 2025.7.4` resolves to) and the VL.StandardLibs sources.

* **Clip:** `SKCanvas.ClipRect(rect, op, antialias)`, `ClipRoundRect`, `ClipPath`, all in the current (transformed) space, restored with the save they were made in.
* **Offscreen group:** `SKCanvas.SaveLayer(SKRect bounds, SKPaint paint)` / `SaveLayer(SKPaint)`. Everything drawn until the matching restore goes to an offscreen layer, then composited once with the paint: alpha (opacity), `ImageFilter` (blur, shadow, …), `ColorFilter`, `BlendMode`. `bounds` is a hint that limits the offscreen size; null means the whole current clip.
* **No backdrop filter in 2.88:** `SKCanvasSaveLayerRec` with `Backdrop` (frosted glass, blurring what is *behind* a node) only exists in SkiaSharp 3.x. Out of reach until VL moves to it.
* **Pictures:** `SKPictureRecorder` + `DrawPicture(picture, matrix, paint)` record and replay a layer. VL.Skia's `DeferredLayer` and `PictureRecorder` do that with a huge cull rect and `CallerInfo.InRenderer(...)`.
* **Precompose (VL.Skia):** per the VL.Skia release notes, "layers can be precomposed (leaving the canvas unchanged) and then applied with other layers… prepare all of them (precompose) and then blur the whole scene at once (SetImageFilter > Blur)", versus blurring each primitive (SetMaskFilter > Blur). It lives in the `.vl` patches, which are out of scope for us; its exact inputs and outputs need confirming (§ 9).
* **Paint hack:** `CallerInfo.WithRenderPaintHack(Func<object, object>)` (`HackRendering` node) lets downstream rewrite the paint of every upstream VL.Skia drawing node. It is per primitive, so overlapping primitives show through each other; it is not a group effect.
* **Layers are plain objects:** `ILayer { Render, Notify, Bounds }`. `Group`, `SetRender`, `TweakCallerInfo` and friends wrap an input layer. A Fu node is an `ILayer` itself (`IRenderable : ILayer`).

## 3. Clipping

### 3.1 Shape

```text
save
concat LocalTransformation
Layer?.Render(inner)                  own layer: background, border — not clipped
if Overflow != Visible:
    ClipRect(clip box, antialias)
children...
restore
```

* **Clip box:** CSS clips to the **padding box**: the node's box minus its border, read from `GetComputedBorder` after layout. The alternative is the full layout box. Padding box is recommended; it lets a border drawn by the own layer stay outside the clip.
* **Own layer unclipped:** matches CSS, where `overflow` clips the content, not the element's own background or border. The alternative is to clip both.
* **Antialiasing:** `antialias: true` gives soft edges under rotation or fractional positions, at some cost; `false` snaps to pixels. Make it a default, and possibly a config switch later.
* **Rounded clips:** need a corner-radius style, which Yoga doesn't have. Later, `ClipRoundRect` with the same radii the own layer draws with.
* **Scroll:** clips like Hidden. The scroll offset and the content extent (the union of the children's margin boxes; Yoga doesn't compute it) belong to the interaction phase.

### 3.2 Side effects

* **Hit testing:** `Notify` must reject positions outside the clip before routing to children; otherwise invisible parts stay clickable.
* **Culling:** with a clip, children whose bounds miss it can be skipped (`canvas.QuickReject`). This is an optional optimisation.
* **Bounds:** `Bounds` is the node's own box today, so it isn't affected. A future subtree bounds (§ 4.3) must be clipped.
* **Where the value comes from:** read `GetOverflow()` from the committed Yoga style, like `IsDisplayed` reads `GetDisplay()`. No new style is needed.

## 4. Group effects ("fade/blur the whole upstream")

### 4.1 The problem

Fading a node by lowering each primitive's alpha (paint hack, per-layer `SetAlpha`) is wrong as soon as anything overlaps: overlapping children show through each other and borders double up. Blurring per primitive (`SetMaskFilter`) blurs each shape alone, so gaps and the background bleed in. A group effect needs one offscreen pass over the node **and its subtree**: `SaveLayer(bounds, paint)` around the own layer and the children.

A VL user can already do this for a whole Fu tree: the root is an `ILayer`, so Precompose and SetImageFilter downstream of a root work. **What is missing is the same for an inner node**, because Fu renders children itself, so there is no VL.Skia chain between a node and its subtree for the user to wrap.

### 4.2 Options

**E1: Fu-native effect styles.** `SetOpacity`, `SetImageFilter`, `SetColorFilter`, `SetBlendMode`, as non-Yoga styles staged onto the node like `TransformStyle` (`StageTransformation` pattern). The renderer builds one `SKPaint` from them and wraps the node in `SaveLayer` only when one is set.

```text
save
concat LocalTransformation
if effect: SaveLayer(effectBounds, effectPaint)
Layer?.Render(inner)
if clip: ClipRect(...)
children...
restore                               (composites the layer once)
```

* **Pros:** cheap when unused, styles and composes like everything else, and Notify is unaffected since geometry is unchanged.
* **Cons:** Fu wraps each Skia effect itself; the filters are VL.Skia objects (`SKImageFilter`, `SKColorFilter`) passed through pins.
* **Order:** effect outside, clip inside. This matches CSS, where `filter` and `opacity` apply to the element after its overflow clip.

**E2: Composer hook (host any VL.Skia layer node).** The node exposes its subtree as a stable `ILayer` (`SubtreeLayer`: own layer plus children, in node space) and takes an optional composer `Func<ILayer, ILayer>`. When set, the node renders and notifies through `composer(SubtreeLayer)` instead of the subtree directly.

```csharp
public void SetComposer(Func<ILayer, ILayer>? composer);  // VL: a Func region; Precompose, SetImageFilter, … inside
```

* **Pros:** any VL.Skia node, including Precompose and future ones, can wrap a Fu subtree. Render and Notify go through the same returned layer, so a composer that moves geometry stays consistent for hit testing.
* **Cons:** VL patch code runs inside Render, which `SetRender` already does. State inside the region lives with the delegate. Bounds and layout don't know about the composer; a composer that scales or moves breaks the "layout = what you see" rule.
* **Needs checking:** how VL instantiates process nodes inside a Func region over frames (one instance per delegate instance?), and whether calling the function every frame allocates.

**E3: Content output plus composite input (feedback).** `Content` outputs `SubtreeLayer`; `SetComposite(ILayer)` takes the wrapped result back. It is the same idea as E2 without a delegate, but the patch forms a loop on one process node. Whether VL accepts the loop across two fragments, and in which frame the composite takes effect, needs checking. Likely worse ergonomics than E2.

**E4: Paint hack.** Pass `caller.WithRenderPaintHack(...)` to the subtree to scale alpha. No offscreen pass, but it is per primitive (wrong for overlap, § 4.1) and only reaches drawing nodes that honour the hack. Not recommended, except possibly as an optimisation for a leaf with one layer and no children.

**Recommendation to evaluate:** E1 for the common effects (opacity at least), E2 as the escape hatch for Precompose and anything else. They compose: the effect paint wraps the composed result.

### 4.3 Effect bounds and cost

* **`SaveLayer` bounds:**
  * With a clip, use the clip box.
  * Without one, use either null (the whole current clip: a full-screen offscreen per node, expensive) or a cached **subtree bounds** (union of the node's box and its descendants' bounds, in node space, kept up to date on layout and transform changes).
  * Image filters reach outside the content (blur radius, shadow offset); the experiments must confirm Skia outsets the layer for the filter, or that we must.
* **Cost:** one offscreen allocation per affected node per frame. GPU backends pool them, but many nested effects multiply the cost. Skip the layer when opacity is 1 and nothing else is set. Skip rendering when opacity is 0, and decide whether the node then still receives input (CSS: yes).
* **Caching (future):** a static subtree could render once into an `SKPicture` (as `DeferredLayer` does) and replay. VL layers have no change notification, so invalidation is the hard part. Out of scope; noted for later.

## 5. Z-index

### 5.1 Options

**Z1: Sibling-scoped z-index** (React Native semantics). An integer style; each node paints its children in stable order of z-index (ties keep tree order). Notify walks the same order in reverse.

* Every node is its own stacking context, so a child can never escape above its parent's siblings. That is simple to reason about.
* Cache the sorted order and invalidate it when children or a child's z-index change. Most nodes have all zeros, so the fast path is the tree order itself.

**Z2: CSS stacking contexts.** z-index on positioned elements competes across the whole stacking context, and opacity, transforms and filters create new contexts. Powerful, but complex, and surprising in a node-based tool. Not recommended.

**Z3: Overlay / portal.** A node (dropdown, tooltip, modal) renders after the whole tree, at the root, with its accumulated transform, and is notified first. This solves "above everything" without global z-index, and clips do not apply to it, which is usually what popups want.

**Recommendation to evaluate:** Z1 now (cheap, local), Z3 later with popups and interaction. Z-index goes in the style chain, staged onto the node like the transform. `PositionType.Absolute` plus z-index covers most "on top" cases inside a container.

### 5.2 Interplay

* Clipping applies to raised children too (they're still inside the parent). Escaping a clip is Z3's job.
* Group effects wrap the parent's children in their painted order.

## 6. Render and Notify order with everything

```text
Render(caller)
  root: capture viewport
  skip if not laid out, display none, (opacity 0?)
  save
  concat LocalTransformation
  [SaveLayer(effectBounds, effectPaint)]                      § 4, E1
  composer is null
      ? Layer?.Render(inner); [ClipRect]; children by z-order   § 3, § 5
      : composer(SubtreeLayer).Render(inner)                   § 4, E2
  restore

Notify(n, caller)                                             (interaction phase)
  skip if display none
  map position into node space (inverse LocalTransformation)
  outside clip → don't route to children
  children in reverse z-order; first handler wins
  own layer / behaviour
```

## 7. Where things live

| Feature | Source of the value | Applied in |
|---|---|---|
| Display none | Yoga style (done) | `Renderable.Render` (done), `Notify` (interaction) |
| Clip | Yoga `Overflow` (done) | `Renderable.Render`, `Notify` |
| Opacity, image filter, color filter, blend mode | New non-Yoga styles, staged like the transform | `Renderable.Render` |
| Composer | `FuNode` pin (a delegate isn't a style) | `Renderable.Render` / `Notify` |
| Z-index | New non-Yoga style, staged | `Renderable` child order (cached) |

## 8. Investigation plan (experiments)

Pixel tests in `VL.Fu.Tests` that render to an `SKBitmap` and compare pixels:

1. **Group vs per-primitive opacity:** two overlapping children under a 50 % parent. With SaveLayer the overlap has the same colour as the non-overlap; with the paint hack it doesn't.
2. **Clip under rotation:** rotated parent with Overflow Hidden. Children are clipped in node space; antialias on vs off at the edges.
3. **Blur bounds:** SaveLayer with a blur image filter and bounds equal to the node box. Is the blur cut at the box edge? Do we need to outset by about 3σ?
4. **Effect plus clip order:** blur and clip together; the blurred edge shows outside the clip (effect outside, clip inside).
5. **Cost:** frame time for N = 100 and 1 000 nodes, with SaveLayer per node vs none; null bounds vs node bounds.
6. **Z1 ordering:** paint and notify order with ties and negative values; the cache is invalidated on child and z-index changes.

In vvvv (manual, by you):

7. **Precompose:** its inputs and outputs, whether it records a picture or renders an image, and whether it keeps the canvas transform.
8. **E2 ergonomics:** a `Func<ILayer, ILayer>` region with Precompose plus SetImageFilter inside. Does state survive frames, and is there per-frame allocation or flicker?
9. **E3 feasibility:** does VL accept the content-to-composite loop on one process node across two fragments?

## 9. Questions to answer before building

1. **Clip box:** padding box (CSS) or the full layout box? Own layer clipped or not?
2. **Clip antialiasing:** default on or off?
3. **Effects:** start with E1 opacity only, or the full set (image filter, color filter, blend mode) at once?
4. **Precompose hosting:** E2 composer delegate, E3 feedback, or skip until E1 proves insufficient?
5. **Opacity 0:** still receives input (CSS) or not?
6. **Z-index:** Z1 sibling-scoped now, Z3 overlay later. Is that acceptable, or is overlay needed first (dropdowns)?
7. **Precompose details:** can you describe its pins, since the `.vl` sources are off limits for us?
