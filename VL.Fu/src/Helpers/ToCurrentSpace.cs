using Stride.Core.Mathematics;
using VL.Fu.Core.Repository;
using VL.Fu.Extensions;
using VL.Fu.Services;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Helpers
{
    public static class Space
    {
        public static Vector2 ToCurrentSpace(
            IRepositoryProvider provider,
            Vector2 value,
            CommonSpace fromSpace
        )
        {
            var service = provider?.GetService<ViewportService>();

            if (service is ViewportService viewportService)
            {
                var resolution = new Vector2(service.Resolution.X, service.Resolution.Y);

                var rawPixelValue = value.ToRawPixels(
                    fromSpace,
                    resolution,
                    service.DIPFactor,
                    service.PixelFactor
                );

                return viewportService.Space switch
                {
                    CommonSpace.Normalized => rawPixelValue.ToNormalizedSpace(
                        service.Bounds.Size.ToVector2()
                    ),
                    CommonSpace.DIP => rawPixelValue.ToCenteredDIPSpace(
                        resolution,
                        service.DIPFactor
                    ),
                    CommonSpace.DIPTopLeft => rawPixelValue.ToDIPTopLeftSpace(service.DIPFactor),
                    CommonSpace.PixelTopLeft => rawPixelValue.ToPixelTopLeftSpace(
                        service.PixelFactor
                    ),
                };
            }

            return value;
        }

        /// <summary>
        /// Converts a RectangleF value from a specified source space into the viewport's current coordinate space.
        /// </summary>
        /// <param name="provider">The repository provider to get services from.</param>
        /// <param name="value">The RectangleF value to convert.</param>
        /// <param name="fromSpace">The space the original value is in.</param>
        /// <returns>The converted RectangleF in the viewport's current space.</returns>
        /// <summary>
        /// Converts a RectangleF value from a specified source space into the viewport's current coordinate space.
        /// </summary>
        /// <param name="provider">The repository provider to get services from.</param>
        /// <param name="value">The RectangleF value to convert.</param>
        /// <param name="fromSpace">The space the original value is in.</param>
        /// <returns>The converted RectangleF in the viewport's current space.</returns>
        public static RectangleF ToCurrentSpace(
            IRepositoryProvider provider,
            RectangleF value,
            CommonSpace fromSpace
        )
        {
            var service = provider?.GetService<ViewportService>();

            if (service is not ViewportService viewportService)
            {
                return value;
            }

            var resolution = new Vector2(service.Resolution.X, service.Resolution.Y);

            // A rectangle is defined by two points. The most robust way to convert it is
            // to convert both points to the common pivot space (raw pixels).
            var topLeftInRawPixels = value.TopLeft.ToRawPixels(
                fromSpace,
                resolution,
                viewportService.DIPFactor,
                viewportService.PixelFactor
            );

            var bottomRightInRawPixels = value.BottomRight.ToRawPixels(
                fromSpace,
                resolution,
                viewportService.DIPFactor,
                viewportService.PixelFactor
            );

            // --- CORRECTED ---
            // Construct the rectangle using Left, Top, Width, and Height.
            var rawPixelRect = new RectangleF(
                topLeftInRawPixels.X,
                topLeftInRawPixels.Y,
                bottomRightInRawPixels.X - topLeftInRawPixels.X,
                bottomRightInRawPixels.Y - topLeftInRawPixels.Y
            );

            // Now, convert the raw pixel rectangle to the final target space.
            return viewportService.Space switch
            {
                CommonSpace.Normalized => rawPixelRect.ToNormalizedSpace(
                    new Int2((int)viewportService.Resolution.X, (int)viewportService.Resolution.Y)
                ),
                CommonSpace.DIP => rawPixelRect.ToCenteredDIPSpace(
                    new Int2((int)viewportService.Resolution.X, (int)viewportService.Resolution.Y),
                    viewportService.DIPFactor
                ),
                CommonSpace.DIPTopLeft => rawPixelRect.ToDIPTopLeftSpace(viewportService.DIPFactor),
                CommonSpace.PixelTopLeft => rawPixelRect.ToPixelTopLeftSpace(
                    viewportService.PixelFactor
                ),
                _ => value,
            };
        }

        /// <summary>
        /// Converts a float value (treated as a distance) from a specified source space
        /// into the viewport's current coordinate space.
        /// </summary>
        /// <param name="provider">The repository provider to get services from.</param>
        /// <param name="value">The float value to convert.</param>
        /// <param name="fromSpace">The space the original value is in.</param>
        /// <returns>The converted float value in the viewport's current space.</returns>
        public static float ToCurrentSpace(
            IRepositoryProvider provider,
            float value,
            CommonSpace fromSpace
        )
        {
            var service = provider?.GetService<ViewportService>();

            if (
                service is not ViewportService viewportService
                || fromSpace == viewportService.Space
            )
            {
                return value;
            }

            // --- CORRECTED AND SIMPLIFIED LOGIC ---
            // First, convert the source distance to a raw pixel distance.
            float valueInRawPixels;
            switch (fromSpace)
            {
                case CommonSpace.PixelTopLeft:
                    valueInRawPixels = value * viewportService.PixelFactor;
                    break;
                case CommonSpace.DIPTopLeft:
                case CommonSpace.DIP: // A distance in centered DIP is the same as top-left DIP
                    valueInRawPixels = value * viewportService.DIPFactor;
                    break;
                case CommonSpace.Normalized:
                    // In normalized space, Y-axis is [-1, 1], so total height is 2 units.
                    // The value is a fraction of the total normalized height (2).
                    // We treat it as a ratio of the screen height.
                    valueInRawPixels = (value / 2f) * viewportService.Resolution.Y;
                    break;
                default:
                    return value;
            }

            // Now, convert the raw pixel distance to the target space distance.
            switch (viewportService.Space)
            {
                case CommonSpace.PixelTopLeft:
                    return valueInRawPixels / viewportService.PixelFactor;
                case CommonSpace.DIPTopLeft:
                case CommonSpace.DIP:
                    return valueInRawPixels / viewportService.DIPFactor;
                case CommonSpace.Normalized:
                    return (valueInRawPixels / viewportService.Resolution.Y) * 2f;
                default:
                    return value;
            }
        }
    }
}
