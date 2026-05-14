using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
using VL.Skia;

namespace VL.Fu.Property
{
    [ProcessNode(Name = "Responsive (Size)")]
    public class ResponsiveSize : ResponsiveContexted<Vector2, ResponsivePropertySize>
    {
        public ResponsiveSize(NodeContext nodeContext)
            : base(nodeContext, Vector2.Zero, Constants.DefaultResponsiveSpace) { }

        protected override ResponsivePropertySize Create(
            NodeContext nodeContext,
            Vector2 initialValue,
            CommonSpace initialSpace
        )
        {
            return new ResponsivePropertySize(nodeContext, initialValue, initialSpace);
        }
    }

    [ProcessNode(Name = "Responsive (Position)")]
    public class ResponsivePosition : ResponsiveContexted<Vector2, ResponsivePropertyPosition>
    {
        public ResponsivePosition(NodeContext nodeContext)
            : base(nodeContext, Vector2.Zero, Constants.DefaultResponsiveSpace) { }

        protected override ResponsivePropertyPosition Create(
            NodeContext nodeContext,
            Vector2 initialValue,
            CommonSpace initialSpace
        )
        {
            return new ResponsivePropertyPosition(nodeContext, initialValue, initialSpace);
        }
    }

    [ProcessNode(Name = "Responsive (Size Downstream)")]
    public class ResponsiveSizeDownstream : ResponsiveDownstream<Vector2, ResponsivePropertySize>
    {
        public ResponsiveSizeDownstream()
            : base(default, Constants.DefaultResponsiveSpace) { }

        protected override ResponsivePropertySize Create(
            IContextProvider provider,
            Vector2 initialValue,
            CommonSpace initialSpace
        ) => new(provider, initialValue, initialSpace);
    }

    [ProcessNode(Name = "Responsive (Position Downstream)")]
    public class ResponsivePositionDownstream
        : ResponsiveDownstream<Vector2, ResponsivePropertyPosition>
    {
        public ResponsivePositionDownstream()
            : base(default, Constants.DefaultResponsiveSpace) { }

        protected override ResponsivePropertyPosition Create(
            IContextProvider provider,
            Vector2 initialValue,
            CommonSpace initialSpace
        ) => new(provider, initialValue, initialSpace);
    }
}
