using Fu.Core.Interaction;
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using VL.Skia;

namespace Fu.Core.Rendering
{
    public interface IRenderable : ILayer
    {
        ILayer? Layer { get; }
        void SetLayer(ILayer layer);
    }

    /// <summary>
    /// Closes the element chain: every node in a Fu tree is a <see cref="Renderable"/>,
    /// so <see cref="Tree.Traversable{TSelf}.Parent"/> and <see cref="Tree.Traversable{TSelf}.Children"/> are typed as such.
    /// </summary>
    public abstract class Renderable : Interactable<Renderable>, IRenderable
    {
        private RectangleF? _bounds;

        /// <summary>
        /// The node's rectangle in its parent's space, or in the caller's space for a root, like every VL.Skia layer.
        /// It follows <see cref="Fu.Core.Layout.ILayoutable.Transformation"/>, so a rotated node reports the box around it.
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
            throw new NotImplementedException();
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
