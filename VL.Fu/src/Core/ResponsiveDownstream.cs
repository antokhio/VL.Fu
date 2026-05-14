using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
using VL.Skia;

namespace VL.Fu.Core
{
    /// <summary>
    /// Base class for Responsive Property process nodes used outside of region.
    /// </summary>
    [ProcessNode]
    public abstract class ResponsiveDownstream<TValue, TProp>
        where TProp : ResponsiveProperty<TValue>
    {
        protected TProp? _value;
        protected bool _invalidate = true;

        protected readonly TValue _defaultValue;
        protected readonly CommonSpace _defaultSpace;

        protected ResponsiveDownstream(TValue defaultValue, CommonSpace defaultSpace)
        {
            _defaultValue = defaultValue;
            _defaultSpace = defaultSpace;
        }

        protected abstract TProp Create(
            IContextProvider provider,
            TValue initialValue,
            CommonSpace initialSpace
        );

        public void Update(IContextProvider provider)
        {
            if (_invalidate)
            {
                _value = Create(provider, _defaultValue, _defaultSpace);
                _invalidate = false;
            }
        }

        public void SetValue(TValue value) => _value?.SetValue(value);

        public void SetSpace(CommonSpace space = Constants.DefaultSpace) => _value?.SetSpace(space);

        public TValue Output => _value != null ? _value.Value : _defaultValue;
    }
}
