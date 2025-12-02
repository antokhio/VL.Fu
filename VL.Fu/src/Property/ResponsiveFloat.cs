using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Context;
using VL.Fu.Core.Property;
using VL.Skia;

namespace VL.Fu.Property
{
    [ProcessNode(Name = "Responsive (Float)")]
    public class ResponsiveFloat : ResponsiveContexted<float, ResponsivePropertyFloat>
    {
        public ResponsiveFloat(NodeContext nodeContext)
            : base(nodeContext, Constants.DefaultResponsiveFloat, Constants.DefaultResponsiveSpace)
        { }

        protected override ResponsivePropertyFloat Create(
            NodeContext nodeContext,
            float initialValue,
            CommonSpace initialSpace
        )
        {
            return new ResponsivePropertyFloat(nodeContext, initialValue, initialSpace);
        }

        public new void SetValue(float value = Constants.DefaultResponsiveFloat) =>
            _value.SetValue(value);
    }

    [ProcessNode(Name = "Responsive (Float Downstream)")]
    public class ResponsiveFloatDownstream : ResponsiveDownstream<float, ResponsivePropertyFloat>
    {
        public ResponsiveFloatDownstream()
            : base(Constants.DefaultResponsiveFloat, Constants.DefaultResponsiveSpace) { }

        protected override ResponsivePropertyFloat Create(
            IContextProvider provider,
            float initialValue,
            CommonSpace initialSpace
        ) => new(provider, initialValue, initialSpace);

        public new void SetValue(float value = Constants.DefaultResponsiveFloat) =>
            _value?.SetValue(value);
    }
}
