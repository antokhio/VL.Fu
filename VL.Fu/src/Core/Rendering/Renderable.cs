using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Tree;
using VL.Model;
using VL.Skia;
using YogaSharp;

namespace VL.Fu.Core.Rendering
{
    public interface IRenderable : IInteractable, ILayer
    {
        ILayer? Layer { get; }
        void SetLayer(ILayer layer);
    }

    /// <summary>
    /// Last generic layer of the element chain: draws the node as a VL.Skia layer.
    /// <see cref="Region.RegionProvider"/> closes the chain at <see cref="IFuNode"/>, the type every node of a Fu tree has.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Renderable<TSelf> : Interactable<TSelf>, IRenderable
        where TSelf : class, ITraversable<TSelf>, IRenderable
    {
        private RectangleF? _explicitBounds;
        private RectangleF? _computedBounds;
        private RectangleF? _viewportArea;

        /// <summary>
        /// The node's rectangle in its parent's space, or in the caller's space for a root, like every VL.Skia layer.
        /// Bounds set with <see cref="SetBounds"/> are returned as they are, like VL.Skia's SetRender and InViewport do.
        /// Otherwise they are computed from the layout and follow <see cref="VL.Fu.Core.Layout.ILayoutable.Transformation"/>,
        /// so a rotated node reports the box around it; null until the node was laid out, since its place is unknown before that.
        /// </summary>
        public RectangleF? Bounds => _explicitBounds ?? _computedBounds;

        /// <summary>
        /// Declares the node's bounds, in the caller's space for a root. A root lays its tree out into them:
        /// their size is the available size and the root is placed at their top-left corner, like content inside InViewport.
        /// Null computes the bounds from the layout again, and a root lays out into the viewport it was last rendered in.
        /// </summary>
        /// <remarks>
        /// On a child they are only reported as <see cref="Bounds"/>, since its parent's layout places it; a warning says so.
        /// Bounds that aren't finite or have a negative size are ignored with a warning.
        /// </remarks>
        public void SetBounds(RectangleF? bounds)
        {
            if (bounds is { } b && !IsValidArea(b))
            {
                this.LogWarning($"Bounds must be finite with a size of at least zero, got {b}. They are ignored.");
                bounds = null;
            }
            else if (bounds is not null && Parent is not null)
            {
                this.LogWarning("Bounds only lay out a root. This node has a parent, which places it; the bounds are only reported.");
            }

            _explicitBounds = bounds;
        }

        private ILayer? _layer;

        public ILayer? Layer => _layer;

        protected Renderable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetLayer(ILayer layer)
        {
            _layer = layer;
        }

        /// <summary>
        /// Draws <see cref="Layer"/> and then the children on top, in order, all in this node's own space,
        /// with (0, 0) at its top-left corner.
        /// </summary>
        /// <remarks>
        /// The local transformation is pushed onto the canvas and onto <see cref="CallerInfo.Transformation"/> alike,
        /// so both stay in sync for layers that read either one; the canvas is restored afterwards.
        /// A root remembers the caller's viewport for its next layout pass (see <see cref="CalculateRootLayout"/>).
        /// A node that was not laid out yet draws nothing, since its place is unknown.
        /// A node with display none draws nothing either, and neither does its subtree, like in the layout.
        /// A node whose overflow is hidden or scroll clips its children to its padding box but not its own layer,
        /// so shadows or outlines it draws stay whole. The clip is antialiased only while the node is rotated or skewed
        /// on the canvas, so axis-aligned clips stay pixel-crisp.
        /// </remarks>
        public void Render(CallerInfo caller)
        {
            if (Parent is null)
                CaptureViewportArea(caller);

            if (!HasLayout || !IsDisplayed)
                return;

            var local = LocalTransformation;
            var canvas = caller.Canvas;
            var save = canvas.Save();
            try
            {
                canvas.Concat(ref local);
                var inner = caller.WithTransformation(caller.Transformation.PreConcat(local));

                _layer?.Render(inner);

                var children = Children;
                if (children.Count == 0)
                    return;

                if (ClipArea is { } area)
                {
                    if (area.Width <= 0 || area.Height <= 0)
                        return;

                    canvas.ClipRect(
                        SKRect.Create(area.X, area.Y, area.Width, area.Height),
                        SKClipOperation.Intersect,
                        antialias: !IsScaleTranslate(canvas.TotalMatrix)
                    );
                }

                for (var i = 0; i < children.Count; i++)
                    children[i].Render(inner);
            }
            finally
            {
                canvas.RestoreToCount(save);
            }
        }

        /// <summary>
        /// Lays the tree out into the bounds set with <see cref="SetBounds"/>, otherwise into the viewport
        /// the root was last rendered in, so the layout follows the window one frame later, as in ImGui.
        /// Does nothing on a child, or on a root that has neither yet.
        /// </summary>
        protected void CalculateRootLayout()
        {
            if (Parent is not null || (_explicitBounds ?? _viewportArea) is not { } area)
                return;

            CalculateLayout(area, YGDirection.LTR);
        }

        /// <summary>
        /// An attached node forgets the viewport it was rendered in as a root, since its parent's layout places it now.
        /// </summary>
        protected override void OnParentChanged(TSelf? oldParent, TSelf? newParent)
        {
            base.OnParentChanged(oldParent, newParent);

            if (newParent is not null)
                _viewportArea = null;
        }

        protected override void OnLocalTransformationChanged()
        {
            base.OnLocalTransformationChanged();

            if (!HasLayout)
                return;

            var layout = Layout;
            _computedBounds = LocalTransformation
                .MapRect(SKRect.Create(layout.Width, layout.Height))
                .ToRectangleF_();
        }

        /// <summary>
        /// Maps the viewport, which is in window pixels, back into the caller's space.
        /// A caller transformation that can't be inverted keeps the last area instead of producing NaN.
        /// </summary>
        private void CaptureViewportArea(CallerInfo caller)
        {
            if (!caller.Transformation.TryInvert(out var inverse))
                return;

            var area = inverse.MapRect(caller.ViewportBounds).ToRectangleF_();
            if (IsValidArea(area))
                _viewportArea = area;
        }

        private static bool IsScaleTranslate(SKMatrix m) =>
            m.SkewX == 0 && m.SkewY == 0 && m.Persp0 == 0 && m.Persp1 == 0 && m.Persp2 == 1;

        private static bool IsValidArea(RectangleF area) =>
            float.IsFinite(area.X)
            && float.IsFinite(area.Y)
            && float.IsFinite(area.Width)
            && float.IsFinite(area.Height)
            && area.Width >= 0
            && area.Height >= 0;
    }
}
