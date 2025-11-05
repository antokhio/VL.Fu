using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Skia;

namespace VL.Fu.Core
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class RenderingBase : TreeNodeBase, IRendering
    {
        protected ILayer? _layer;
        public RectangleF? Bounds => _layer?.Bounds;

        [Fragment(Order = PinOrder.Layer)]
        public void SetLayer(ILayer layer)
        {
            _layer = layer;
        }

        public void Render(CallerInfo caller)
        {
            _layer?.Render(caller);

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
