using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Skia;

namespace VL.Fu.Core
{
    /// <summary>
    /// A base class for nodes that participate in the rendering pass.
    /// It renders its own content (via the Layer pin) and then recursively renders its children.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Renderable : Stylable, IRendering
    {
        protected ILayer? _layer;

        // Expose the bounds of the local layer.
        // Note: This does not currently include the bounds of the children.
        public RectangleF? Bounds => Layout;

        protected Renderable(NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// Assigns the visual content for this specific node.
        /// </summary>
        [Fragment(Order = PinOrder.Layer)]
        public void SetLayer(ILayer? layer)
        {
            _layer = layer;
        }

        /// <summary>
        /// Executes the rendering pass.
        /// Renders the local layer first, followed by all children.
        /// </summary>
        public virtual void Render(CallerInfo caller)
        {
            // Draw self
            _layer?.Render(caller);

            // Draw hierarchy
            foreach (var child in Children)
            {
                if (child is ILayer layer)
                {
                    layer.Render(caller);
                }
            }
        }
    }
}
