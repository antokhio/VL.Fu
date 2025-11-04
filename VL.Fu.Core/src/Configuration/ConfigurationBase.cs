using VL.Fu.Core.Common;
using VL.Fu.Core.InstanceId;
using VL.Fu.Core.Property;
using VL.Lib.Reactive;
using VL.Skia;

namespace VL.Fu.Core.Configuration
{
    public abstract class ConfigurationBase : InstanceIdBase
    {
        /// <summary>
        /// A constant factor for scaling pixel values, typically used for non-DPI-aware calculations.
        /// </summary>
        public readonly int PixelFactor = Constants.DefaultPixelFactor;

        // --- Reactive DIP Factor ---
        protected readonly ChannelProperty<int> _dipFactor = new(Constants.DefaultDIPFactor);

        /// <summary>
        /// A reactive channel providing the current Device Independent Pixel factor.
        /// </summary>
        public IChannel<int> DIPFactor => _dipFactor.Channel;

        /// <summary>
        /// Sets a new DIP factor, which will be broadcast to all subscribers if it has changed.
        /// </summary>
        public void SetDIPFactor(int dipFactor) => _dipFactor.TrySetValue(dipFactor);

        // --- Reactive CommonSpace ---
        protected readonly ChannelProperty<CommonSpace> _space = new(Constants.DefaultCommonSpace);

        /// <summary>
        /// A reactive channel providing the current coordinate space.
        /// </summary>
        public IChannel<CommonSpace> Space => _space.Channel;

        /// <summary>
        /// Sets a new coordinate space, which will be broadcast to all subscribers if it has changed.
        /// </summary>
        public void SetSpace(CommonSpace space = Constants.DefaultCommonSpace) =>
            _space.TrySetValue(space);
    }
}
