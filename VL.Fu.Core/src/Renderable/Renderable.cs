using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
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

        private SKMatrix _transformation = SKMatrix.Identity;

        public void SetTransformation(SKMatrix? transformation)
        {
            _transformation = transformation ?? SKMatrix.Identity;
        }

        protected Renderable(NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// Assigns the visual content for this specific node.
        /// </summary>
        public virtual void SetLayer(ILayer? layer)
        {
            _layer = layer;
        }

        /// <summary>
        /// Executes the rendering pass.
        /// Renders the local layer first, followed by all children.
        /// </summary>
        public virtual void Render(CallerInfo caller)
        {
            var us = caller.PushTransformation(_transformation);
            us.Canvas.SetMatrix(us.Transformation);
            // Draw self
            _layer?.Render(us);

            // Draw hierarchy
            foreach (var child in Children)
            {
                if (child is ILayer layer)
                {
                    layer.Render(us);
                }
            }

            caller.Canvas.SetMatrix(caller.Transformation);
        }
    }

    internal static class CallerInfoExtensions
    {
        public static CallerInfo PushTransformation(this CallerInfo callerInfo, SKMatrix relative)
        {
            SKMatrix target = callerInfo.Transformation;
            return callerInfo with { Transformation = target.PreConcat(relative) };
        }
    }
}
