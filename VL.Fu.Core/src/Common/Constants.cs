using VL.Skia;

namespace VL.Fu.Core.Common
{
    public struct Constants
    {
        public static readonly string RootContextProviderName = "FuRootContextProvider";

        public const int MousePointerId = -1;

        public const int DefaultDIPFactor = 100;

        public const int DefaultPixelFactor = 100;

        public const CommonSpace DefaultSpace = CommonSpace.Normalized;
        public const CommonSpace DefaultResponsiveSpace = CommonSpace.DIP;

        public const ScalingMode DefaultScalingMode = ScalingMode.DIPAndPixel;

        public const float DefaultScaling = 1.0f;

        public static readonly TimeSpan DefaultFocusGracePeriod = TimeSpan.FromMilliseconds(200);
        public static readonly TimeSpan DefaultTouchActivityTimeout = TimeSpan.FromMilliseconds(
            150
        );
        public static readonly TimeSpan DefaultInputActivityTimeout = TimeSpan.FromMilliseconds(
            150
        );

        public const float DefaultResponsiveFloat = 0.01f;
    }
}
