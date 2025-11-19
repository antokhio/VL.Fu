using Stride.Core.Mathematics;
using VL.Fu.Core.Common;
using VL.Lib.Mathematics;
using VL.Skia;

namespace VL.Fu.Core.Extensions
{
    public static class ScalingModeExtensions
    {
        /// <summary>
        /// Applies scaling to a pixel-based value according to the scaling mode.
        /// </summary>
        /// <param name="pixelFactor">Pixel-based measurement value</param>
        /// <param name="scalingMode">Scaling mode that determines which coordinate spaces should be scaled</param>
        /// <param name="scaling">Display scaling factor to apply</param>
        /// <returns>Scaled pixel value if scaling mode includes pixels, otherwise original value</returns>
        public static int WithPixelFactorScalingMode(
            this int pixelFactor,
            ScalingMode scalingMode,
            float scaling
        ) =>
            scalingMode switch
            {
                ScalingMode.DIPAndPixel or ScalingMode.Pixel => (int)
                    Math.Round(pixelFactor * scaling),
                ScalingMode.DIP or ScalingMode.None => pixelFactor,
            };

        /// <summary>
        /// Applies scaling to a DIP-based value according to the scaling mode.
        /// </summary>
        /// <param name="dipFactor">DIP-based (Device Independent Pixel) measurement value</param>
        /// <param name="scalingMode">Scaling mode that determines which coordinate spaces should be scaled</param>
        /// <param name="scaling">Display scaling factor to apply</param>
        /// <returns>Scaled DIP value if scaling mode includes DIP, otherwise original value</returns>
        public static int WithDIPFactorScalingMode(
            this int dipFactor,
            ScalingMode scalingMode,
            float scaling
        ) =>
            scalingMode switch
            {
                ScalingMode.DIPAndPixel or ScalingMode.DIP => (int)Math.Round(dipFactor * scaling),
                ScalingMode.Pixel or ScalingMode.None => dipFactor,
            };

        /// <summary>
        /// Applies scaling to rectangle bounds depending on current space and scaling mode.
        /// </summary>
        /// <param name="bounds">Current view bounds</param>
        /// <param name="currentSpace">Current bounds coordinate space</param>
        /// <param name="scalingMode">Scaling mode that determines which spaces should be scaled</param>
        /// <param name="scaling">Display scaling factor</param>
        /// <returns>Scaled rectangle if scaling applies, otherwise original bounds</returns>
        public static RectangleF WithScalingMode(
            this RectangleF bounds,
            CommonSpace currentSpace,
            ScalingMode scalingMode,
            float scaling
        )
        {
            bool shouldScale = currentSpace switch
            {
                CommonSpace.DIP or CommonSpace.DIPTopLeft => scalingMode
                    is ScalingMode.DIP
                        or ScalingMode.DIPAndPixel,

                CommonSpace.PixelTopLeft => scalingMode
                    is ScalingMode.Pixel
                        or ScalingMode.DIPAndPixel,

                CommonSpace.Normalized => false,

                _ => false,
            };

            if (shouldScale)
            {
                RectangleNodes.ScaleUniform(ref bounds, scaling, out var rect);
                return rect;
            }

            return bounds;
        }
    }
}
