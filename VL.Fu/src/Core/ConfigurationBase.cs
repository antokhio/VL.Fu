using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
using VL.Lib.Reactive;
using VL.Skia;

namespace VL.Fu.Core
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class ConfigurationBase : RepositoryProvider
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
        public IChannel<int> DIPFactor => _dipFactor;

        /// <summary>
        /// Sets a new DIP factor, which will be broadcast to all subscribers if it has changed.
        /// </summary>
        [Fragment]
        public void SetDIPFactor(int dipFactor = Constants.DefaultDIPFactor) =>
            _dipFactor.EnsureValue(dipFactor);

        // --- Reactive CommonSpace ---
        protected readonly ChannelProperty<CommonSpace> _space = new(Constants.DefaultCommonSpace);

        /// <summary>
        /// A reactive channel providing the current coordinate space.
        /// </summary>
        public IChannel<CommonSpace> Space => _space;

        /// <summary>
        /// Sets a new coordinate space, which will be broadcast to all subscribers if it has changed.
        /// </summary>
        [Fragment]
        public void SetSpace(CommonSpace space = Constants.DefaultCommonSpace) =>
            _space.EnsureValue(space);

        protected readonly ChannelProperty<bool> _enabled = new(true);

        /// <summary>
        /// A reactive channel indicating whether the Fu context is active and should handle input.
        /// </summary>
        public IChannel<bool> Enabled => _enabled;

        /// <summary>
        /// Sets whether the Fu context is enabled. When disabled, input handling will cease.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true) => _enabled.EnsureValue(enabled);
    }
}
