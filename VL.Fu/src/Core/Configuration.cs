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
        public readonly int PixelFactor = Constants.DefaultPixelFactor;

        public readonly IChannel<CommonSpace> Space = new ChannelProperty<CommonSpace>(
            Constants.DefaultCommonSpace
        );

        public IChannel<int> DIPFactor = new ChannelProperty<int>(Constants.DefaultDIPFactor);

        public IChannel<DipFactorMode> DipFactorMode = new ChannelProperty<DipFactorMode>(
            Constants.DefaultDipFactorMode
        );

        [Fragment]
        public Configuration(NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// Sets a new DIP factor, which will be broadcast to all subscribers if it has changed.
        /// </summary>
        /// <param name="dipFactor">User specified DIP Factor</param>
        [Fragment]
        public void SetDIPFactor(int dipFactor = Constants.DefaultDIPFactor) =>
            DIPFactor.EnsureValue(dipFactor);

        /// <summary>
        /// Sets a new coordinate space, which will be broadcast to all subscribers if it has changed.
        /// </summary>
        /// <param name="space">User specified CommonSpace to use</param>
        [Fragment]
        public void SetSpace(CommonSpace space = Constants.DefaultCommonSpace) =>
            Space.EnsureValue(space);

        /// <summary>
        /// Sets a new DIP factor, which will be broadcast to all subscribers if it has changed.
        /// </summary>
        /// <param name="dipFactor">User specified DIP Factor</param>
        [Fragment]
        public void SetDIPFactorMode(
            DipFactorMode dipFactorMode = Constants.DefaultDipFactorMode
        ) => DipFactorMode.EnsureValue(dipFactorMode);
    }
}
