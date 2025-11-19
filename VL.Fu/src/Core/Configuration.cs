using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
using VL.Lib.Reactive;
using VL.Skia;

namespace VL.Fu.Core
{
    /// <summary>
    /// Manages incoming layout configuration properties.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Configuration : NotificationsProvider
    {
        /// <summary>
        /// Reactive channel that stores user specified DIP factor.
        /// </summary>
        public readonly IChannel<int> DIPFactor = new ChannelProperty<int>(
            Constants.DefaultDIPFactor
        );

        /// <summary>
        /// Reactive channel that stores user specified pixel factor.
        /// </summary>
        public readonly IChannel<int> PixelFactor = new ChannelProperty<int>(
            Constants.DefaultPixelFactor
        );

        /// <summary>
        /// Reactive channel that stores user specified working space.
        /// </summary>
        public readonly IChannel<CommonSpace> Space = new ChannelProperty<CommonSpace>(
            Constants.DefaultSpace
        );

        /// <summary>
        /// Reactive channel that stores user specified scaling handling.
        /// </summary>
        public readonly IChannel<ScalingMode> ScalingMode = new ChannelProperty<ScalingMode>(
            Constants.DefaultScalingMode
        );

        /// <summary>
        /// Reactive channel that stores whether the input is enabled.
        /// </summary>
        public readonly IChannel<bool> Enabled = new ChannelProperty<bool>(true);

        [Fragment]
        public Configuration(NodeContext nodeContext)
            : base(nodeContext) { }

        /// <param name="dipFactor">User specified DIP Factor</param>
        [Fragment]
        public void SetDIPFactor(int dipFactor = Constants.DefaultDIPFactor) =>
            DIPFactor.EnsureValue(dipFactor);

        /// <param name="pixelFactor">User specified pixel factor</param>
        [Fragment]
        public void SetPixelFactor(int pixelFactor = Constants.DefaultPixelFactor) =>
            PixelFactor.EnsureValue(pixelFactor);

        /// <param name="space">User specified working Space</param>
        [Fragment]
        public void SetSpace(CommonSpace space = Constants.DefaultSpace) =>
            Space.EnsureValue(space);

        /// <param name="scalingMode">Render Scaling handling mode</param>
        [Fragment]
        public void SetScalingMode(ScalingMode scalingMode = Constants.DefaultScalingMode) =>
            ScalingMode.EnsureValue(scalingMode);

        /// <param name="enabled">Whether input is enabled</param>
        [Fragment]
        public void SetEnabled(bool enabled = true) => Enabled.EnsureValue(enabled);
    }
}
