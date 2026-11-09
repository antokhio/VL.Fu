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

        public void Render(CallerInfo caller)
        {
            //throw new NotImplementedException();
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
