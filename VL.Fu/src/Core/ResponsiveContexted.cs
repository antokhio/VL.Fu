using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
using VL.Skia;

namespace VL.Fu.Core
{
    /// <summary>
    /// Base class for Responsive Property process nodes to reduce boilerplate.
    /// </summary>
    [ProcessNode]
    public abstract class ResponsiveContexted<TValue, TProp>
        where TProp : ResponsiveProperty<TValue>
    {
        protected readonly TProp _value;

        protected ResponsiveContexted(
            NodeContext nodeContext,
            TValue initialValue,
            CommonSpace initialSpace
        )
        {
            _value = Create(nodeContext, initialValue, initialSpace);
        }

        protected abstract TProp Create(
            NodeContext nodeContext,
            TValue initialValue,
            CommonSpace initialSpace
        );

        public void SetValue(TValue value) => _value.SetValue(value);

        public void SetSpace(CommonSpace space = Constants.DefaultSpace) => _value.SetSpace(space);

        public TValue Output => _value.Value;
    }
}
