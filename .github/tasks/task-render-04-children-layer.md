# Task 04: ChildrenLayer for Precompose and other layer wrappers

Source: summary §3.2 E2-B, §7 row 3; proposal §4 E2. Depends on: 01.

## Goal

Let a `FuNode` region wrap its own content and its children in any VL.Skia layer node: `Precompose`, `Mask`, an offscreen renderer, a custom shader layer. It needs no new region and no feedback loop, and the wrapping nodes keep their state, because they live in the `FuNode` region, which is already a stateful `IRegionInlay`.

## Design

* `IRenderable` gets `ILayer ChildrenLayer { get; }`. `IFuNode` inherits it, so the node passed into the region exposes it.
* `Renderable` holds one private sealed `ChildrenLayerImpl : ILayer` per node, created once.
  * `Render(caller)` draws the clip (task 01) and the children with the caller it gets. If the user wraps it in a transform, the children move with it.
  * `Bounds` is the node's own box, `(0, 0, Layout.Width, Layout.Height)`, in node space.
  * `Notify` is deferred to interaction.
* `Renderable.Render` keeps a per-pass state: `_rendering` is set around the body, and `_childrenDrawn` is reset at its start.
  * When `ChildrenLayer.Render` runs inside its own node's pass, it draws the children and sets `_childrenDrawn`.
  * After `_layer?.Render(inner)`, the node draws the clip and children itself only if `_childrenDrawn` is still false. A region that leaves `ChildrenLayer` out, or switches it off for a frame, still shows the children, after the layer as today.
* Misuse is ignored with a warning, never thrown:
  * rendered outside its node's pass, e.g. in another node's layer or a second renderer;
  * rendered twice in one pass.
* Task 03 effects stay outside: the effect layer wraps the region's layer, `ChildrenLayer` included.

## Decisions

1. **Name.** `ChildrenLayer`, or `Content` / `Subtree`?
2. **Where the clip goes.** Inside `ChildrenLayer` (recommended), so a wrapped subtree is still clipped by its node's Overflow. The alternative leaves clipping to the wrapper.
3. **Misuse.** Warn and skip (recommended), or draw anyway?

## Tests

* A layer that includes `ChildrenLayer` draws the children once, at the place the layer puts them; for example, a background drawn after `ChildrenLayer` covers the children.
* A layer without `ChildrenLayer` draws the children after the layer, as today.
* Wrapping `ChildrenLayer` in a translating test layer moves the children.
* A test layer that brackets with `SaveLayer(alpha)` and `Restore`, standing in for `Precompose`, fades the children as a group.
* `ChildrenLayer` rendered from another node's layer draws nothing and logs a warning.
* `ChildrenLayer` rendered twice in one layer draws once.

## Deferred

`Notify` through `ChildrenLayer`, with the same pass flag, lands with interaction.
