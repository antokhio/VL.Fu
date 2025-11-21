using VL.Skia;

namespace VL.Fu.Core.Common
{
    public struct Constants
    {
        public const int MousePointerId = -1;

        public const int DefaultDIPFactor = 100;

        public const int DefaultPixelFactor = 100;

        public const CommonSpace DefaultSpace = CommonSpace.Normalized;

        public const ScalingMode DefaultScalingMode = ScalingMode.DIPAndPixel;

        public const float DefaultScaling = 1.0f;

        public static TimeSpan DefaultFocusGracePeriod = TimeSpan.FromMilliseconds(200);
        public static TimeSpan DefaultTouchActivityTimeout = TimeSpan.FromMilliseconds(150);
        public static TimeSpan DefaultInputActivityTimeout = TimeSpan.FromMilliseconds(150);
    }
}
