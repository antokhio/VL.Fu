using VL.Fu.Core.Common;

namespace VL.Fu.Extensions
{
    public static class ScalingModeExtensions
    {
        public static int ToPixelFactor(
            this ScalingMode scalingMode,
            int intialPixelFactor,
            float scaling
        ) =>
            scalingMode switch
            {
                ScalingMode.DIPAndPixel or ScalingMode.Pixel => (int)
                    Math.Round(intialPixelFactor * scaling),
                ScalingMode.DIP or ScalingMode.None => intialPixelFactor,
            };

        public static int ToDIPFactor(
            this ScalingMode scalingMode,
            int intialDipFactor,
            float scaling
        ) =>
            scalingMode switch
            {
                ScalingMode.DIPAndPixel or ScalingMode.DIP => (int)
                    Math.Round(intialDipFactor * scaling),
                ScalingMode.Pixel or ScalingMode.None => intialDipFactor,
            };
    }
}
