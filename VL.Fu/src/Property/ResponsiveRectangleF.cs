using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Context;
using VL.Fu.Core.Property;
using VL.Skia;

namespace VL.Fu.Property
{
    [ProcessNode(Name = "Responsive (Rectangle)")]
    public class ResponsiveRectangleF
        : ResponsiveContexted<RectangleF, ResponsivePropertyRectangleF>
    {
        public ResponsiveRectangleF(NodeContext nodeContext)
            : base(nodeContext, default, Constants.DefaultResponsiveSpace) { }

        protected override ResponsivePropertyRectangleF Create(
            NodeContext nodeContext,
            RectangleF initialValue,
            CommonSpace initialSpace
        )
        {
            return new ResponsivePropertyRectangleF(nodeContext, initialValue, initialSpace);
        }
    }

    [ProcessNode(Name = "Responsive (Rectangle Downstream)")]
    public class ResponsiveRectangleFDownstream
        : ResponsiveDownstream<RectangleF, ResponsivePropertyRectangleF>
    {
        public ResponsiveRectangleFDownstream()
            : base(default, Constants.DefaultResponsiveSpace) { }

        protected override ResponsivePropertyRectangleF Create(
            IContextProvider provider,
            RectangleF initialValue,
            CommonSpace initialSpace
        ) => new(provider, initialValue, initialSpace);
    }
}
