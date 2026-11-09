using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Tree;
using VL.Model;
using VL.Skia;

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
        private RectangleF? _bounds;

        /// <summary>
        /// The node's rectangle in its parent's space, or in the caller's space for a root, like every VL.Skia layer.
        /// It follows <see cref="VL.Fu.Core.Layout.ILayoutable.Transformation"/>, so a rotated node reports the box around it.
        /// Null until the node was laid out, since its place is unknown before that.
        /// </summary>
        public RectangleF? Bounds => _bounds;

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
        /// A node that was not laid out yet draws nothing, since its place is unknown.
        /// </remarks>
        public void Render(CallerInfo caller)
        {
            if (!HasLayout)
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
                for (var i = 0; i < children.Count; i++)
                    children[i].Render(inner);
            }
            finally
            {
                canvas.RestoreToCount(save);
            }
        }

        protected override void OnLocalTransformationChanged()
        {
            base.OnLocalTransformationChanged();

            if (!HasLayout)
                return;

            var layout = Layout;
            _bounds = LocalTransformation
                .MapRect(SKRect.Create(layout.Width, layout.Height))
                .ToRectangleF_();
        }
    }
}
