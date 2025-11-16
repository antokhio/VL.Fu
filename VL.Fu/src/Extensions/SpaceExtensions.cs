using Stride.Core.Mathematics;
using VL.Skia;

namespace VL.Fu.Extensions
{
    /// <summary>
    /// Provides extension methods for converting Vector2 and RectangleF coordinates between different 2D coordinate spaces.
    /// </summary>
    public static class SpaceExtensions
    {
        /// <summary>
        /// Converts a rectangle from pixel coordinates to a normalized, aspect-ratio-corrected space where (0,0) is the center.
        /// The shorter axis of the viewport will range from -1 to 1.
        /// </summary>
        /// <param name="pixelRect">The source rectangle in pixel coordinates.</param>
        /// <param name="size">The total size of the viewport in pixels.</param>
        /// <returns>A new rectangle in normalized coordinate space.</returns>
        public static RectangleF ToNormalizedSpace(this RectangleF pixelRect, Int2 size)
        {
            var p1 = new Vector2(pixelRect.Left, pixelRect.Top).ToNormalizedSpace(size.ToVector());
            var p2 = new Vector2(pixelRect.Right, pixelRect.Bottom).ToNormalizedSpace(
                size.ToVector()
            );
            return new RectangleF(p1.X, p1.Y, p2.X - p1.X, p2.Y - p1.Y);
        }

        /// <summary>
        /// Converts a rectangle from pixel coordinates to a centered DIP (Device Independent Pixel) space.
        /// The coordinates are scaled by the DIP factor, and the origin (0,0) is at the center of the viewport.
        /// </summary>
        /// <param name="rectangleInPixels">The source rectangle in pixel coordinates.</param>
        /// <param name="resolution">The total resolution of the viewport in pixels.</param>
        /// <param name="dipFactor">The factor to scale pixels by (e.g., 100 for a 1:100 scaling).</param>
        /// <returns>A new rectangle in centered DIP space.</returns>
        public static RectangleF ToCenteredDIPSpace(
            this RectangleF rectangleInPixels,
            Int2 resolution,
            float dipFactor
        )
        {
            float totalWidthInUnits = resolution.X / dipFactor;
            float totalHeightInUnits = resolution.Y / dipFactor;

            float halfWidthInUnits = totalWidthInUnits / 2f;
            float halfHeightInUnits = totalHeightInUnits / 2f;

            float normX = (rectangleInPixels.X / dipFactor) - halfWidthInUnits;
            float normY = (rectangleInPixels.Y / dipFactor) - halfHeightInUnits;

            float normWidth = rectangleInPixels.Width / dipFactor;
            float normHeight = rectangleInPixels.Height / dipFactor;

            return new RectangleF(normX, normY, normWidth, normHeight);
        }

        /// <summary>
        /// Converts a rectangle from pixel coordinates to a DIP (Device Independent Pixel) space where (0,0) is the top-left corner.
        /// </summary>
        /// <param name="rectangleInPixels">The source rectangle in pixel coordinates.</param>
        /// <param name="dipFactor">The factor to scale pixels by.</param>
        /// <returns>A new rectangle in top-left DIP space.</returns>
        public static RectangleF ToDIPTopLeftSpace(
            this RectangleF rectangleInPixels,
            float dipFactor
        )
        {
            float scaledX = rectangleInPixels.X / dipFactor;
            float scaledY = rectangleInPixels.Y / dipFactor;
            float scaledWidth = rectangleInPixels.Width / dipFactor;
            float scaledHeight = rectangleInPixels.Height / dipFactor;

            return new RectangleF(scaledX, scaledY, scaledWidth, scaledHeight);
        }

        /// <summary>
        /// Converts a rectangle from one pixel-based system to another, scaled by a given factor, with the origin at the top-left.
        /// </summary>
        /// <param name="rectangleInPixels">The source rectangle in pixel coordinates.</param>
        /// <param name="pixelFactor">The factor to scale the pixel values by.</param>
        /// <returns>A new rectangle in the scaled top-left space.</returns>
        public static RectangleF ToPixelTopLeftSpace(
            this RectangleF rectangleInPixels,
            float pixelFactor
        )
        {
            float scaledX = rectangleInPixels.X / pixelFactor;
            float scaledY = rectangleInPixels.Y / pixelFactor;
            float scaledWidth = rectangleInPixels.Width / pixelFactor;
            float scaledHeight = rectangleInPixels.Height / pixelFactor;

            return new RectangleF(scaledX, scaledY, scaledWidth, scaledHeight);
        }

        /// <summary>
        /// Converts a vector from pixel coordinates to a normalized, aspect-ratio-corrected space where (0,0) is the center.
        /// </summary>
        /// <param name="pixelPosition">The source position in pixel coordinates.</param>
        /// <param name="size">The total size of the viewport in pixels.</param>
        /// <returns>A new vector in normalized coordinate space.</returns>
        public static Vector2 ToNormalizedSpace(this Vector2 pixelPosition, Vector2 size)
        {
            float aspect = size.X / size.Y;
            Vector2 norm;
            norm.X = (pixelPosition.X / size.X) * 2f - 1f;
            norm.Y = (pixelPosition.Y / size.Y) * 2f - 1f;
            if (aspect > 1f)
                norm.X *= aspect;
            else
                norm.Y /= aspect;
            return norm;
        }

        /// <summary>
        /// Converts a vector from pixel coordinates to a centered DIP (Device Independent Pixel) space.
        /// </summary>
        /// <param name="pixelPosition">The source position in pixel coordinates.</param>
        /// <param name="resolution">The total resolution of the viewport in pixels.</param>
        /// <param name="dipFactor">The factor to scale pixels by.</param>
        /// <returns>A new vector in centered DIP space.</returns>
        public static Vector2 ToCenteredDIPSpace(
            this Vector2 pixelPosition,
            Int2 resolution,
            float dipFactor
        )
        {
            float halfWidthInUnits = (resolution.X / dipFactor) / 2f;
            float halfHeightInUnits = (resolution.Y / dipFactor) / 2f;

            float normX = (pixelPosition.X / dipFactor) - halfWidthInUnits;
            float normY = (pixelPosition.Y / dipFactor) - halfHeightInUnits;

            return new Vector2(normX, normY);
        }

        /// <summary>
        /// Converts a vector from pixel coordinates to a DIP (Device Independent Pixel) space where (0,0) is the top-left corner.
        /// </summary>
        /// <param name="pixelPosition">The source position in pixel coordinates.</param>
        /// <param name="dipFactor">The factor to scale pixels by.</param>
        /// <returns>A new vector in top-left DIP space.</returns>
        public static Vector2 ToDIPTopLeftSpace(this Vector2 pixelPosition, float dipFactor)
        {
            float scaledX = pixelPosition.X / dipFactor;
            float scaledY = pixelPosition.Y / dipFactor;

            return new Vector2(scaledX, scaledY);
        }

        /// <summary>
        /// Converts a vector from one pixel-based system to another, scaled by a given factor, with the origin at the top-left.
        /// </summary>
        /// <param name="pixelPosition">The source position in pixel coordinates.</param>
        /// <param name="pixelFactor">The factor to scale the pixel values by.</param>
        /// <returns>A new vector in the scaled top-left space.</returns>
        public static Vector2 ToPixelTopLeftSpace(this Vector2 pixelPosition, float pixelFactor)
        {
            float scaledX = pixelPosition.X / pixelFactor;
            float scaledY = pixelPosition.Y / pixelFactor;

            return new Vector2(scaledX, scaledY);
        }

        /// <summary>
        /// Helper function to convert any space into raw, top-left pixel coordinates.
        /// </summary>
        public static Vector2 ToRawPixels(
            this Vector2 value,
            CommonSpace fromSpace,
            Int2 resolution,
            float dipFactor,
            float pixelFactor
        )
        {
            return fromSpace switch
            {
                // Value is already in our pixel-based unit, scale it up to raw pixels.
                CommonSpace.PixelTopLeft => value * pixelFactor,

                // Value is in DIPs, scale it up to raw pixels.
                CommonSpace.DIPTopLeft => value * dipFactor,

                // Value is in centered DIPs. First, un-center it, then scale up.
                CommonSpace.DIP => UncenterAndScale(value, resolution, dipFactor),

                // Value is in normalized space. First, un-normalize it, then un-apply aspect ratio.
                CommonSpace.Normalized => FromNormalized(value, resolution),

                _ => value, // Should not happen with a valid space
            };
        }

        private static Vector2 UncenterAndScale(
            Vector2 centeredValue,
            Int2 resolution,
            float factor
        )
        {
            var spaceResolution = resolution.ToVector() / factor;
            var halfSpaceResolution = spaceResolution / 2f;

            // Add back the half-resolution to move origin to top-left, then scale up to raw pixels.
            var topLeftValue = centeredValue + halfSpaceResolution;
            return topLeftValue * factor;
        }

        private static Vector2 FromNormalized(Vector2 normalizedValue, Int2 resolution)
        {
            var p = normalizedValue;
            var aspect = resolution.X / resolution.Y;
            if (aspect > 1f)
                p.X /= aspect;
            else
                p.Y *= aspect;
            var pix = new Vector2((p.X + 1f) / 2f * resolution.X, (p.Y + 1f) / 2f * resolution.Y);
            return pix;
        }

        /// <summary>
        /// Converts a Vector2 from a source space to a target space.
        /// </summary>
        /// <param name="value">The vector to convert.</param>
        /// <param name="fromSpace">The source coordinate space.</param>
        /// <param name="toSpace">The target coordinate space.</param>
        /// <param name="resolution">The resolution of the screen/viewport.</param>
        /// <param name="dipFactor">The factor to convert between DIPs and raw pixels.</param>
        /// <param name="pixelFactor">The factor to convert between logical pixels and raw pixels.</param>
        /// <returns>The converted Vector2.</returns>
        public static Vector2 ConvertSpace(
            this Vector2 value,
            CommonSpace fromSpace,
            CommonSpace toSpace,
            Int2 resolution,
            float dipFactor,
            float pixelFactor
        )
        {
            var rawPixelValue = value.ToRawPixels(fromSpace, resolution, dipFactor, pixelFactor);

            return toSpace switch
            {
                CommonSpace.Normalized => rawPixelValue.ToNormalizedSpace(resolution.ToVector()),
                CommonSpace.DIP => rawPixelValue.ToCenteredDIPSpace(resolution, dipFactor),
                CommonSpace.DIPTopLeft => rawPixelValue.ToDIPTopLeftSpace(dipFactor),
                CommonSpace.PixelTopLeft => rawPixelValue.ToPixelTopLeftSpace(pixelFactor),
                _ => value,
            };
        }

        /// <summary>
        /// Converts a RectangleF from a source space to a target space.
        /// </summary>
        /// <param name="value">The rectangle to convert.</param>
        /// <param name="fromSpace">The source coordinate space.</param>
        /// <param name="toSpace">The target coordinate space.</param>
        /// <param name="resolution">The resolution of the screen/viewport.</param>
        /// <param name="dipFactor">The factor to convert between DIPs and raw pixels.</param>
        /// <param name="pixelFactor">The factor to convert between logical pixels and raw pixels.</param>
        /// <returns>The converted RectangleF.</returns>
        public static RectangleF ConvertSpace(
            this RectangleF value,
            CommonSpace fromSpace,
            CommonSpace toSpace,
            Int2 resolution,
            float dipFactor,
            float pixelFactor
        )
        {
            // A rectangle is defined by two points. The most robust way to convert it is
            // to convert both points to the common pivot space (raw pixels).
            var topLeftInRawPixels = value.TopLeft.ToRawPixels(
                fromSpace,
                resolution,
                dipFactor,
                pixelFactor
            );

            var bottomRightInRawPixels = value.BottomRight.ToRawPixels(
                fromSpace,
                resolution,
                dipFactor,
                pixelFactor
            );

            var rawPixelRect = new RectangleF(
                topLeftInRawPixels.X,
                topLeftInRawPixels.Y,
                bottomRightInRawPixels.X - topLeftInRawPixels.X,
                bottomRightInRawPixels.Y - topLeftInRawPixels.Y
            );

            // Now, convert the raw pixel rectangle to the final target space.
            return toSpace switch
            {
                CommonSpace.Normalized => rawPixelRect.ToNormalizedSpace(resolution),
                CommonSpace.DIP => rawPixelRect.ToCenteredDIPSpace(resolution, dipFactor),
                CommonSpace.DIPTopLeft => rawPixelRect.ToDIPTopLeftSpace(dipFactor),
                CommonSpace.PixelTopLeft => rawPixelRect.ToPixelTopLeftSpace(pixelFactor),
                _ => value,
            };
        }

        public static float ConvertSpace(
            this float value,
            CommonSpace fromSpace,
            CommonSpace toSpace,
            Int2 resolution,
            float dipFactor,
            float pixelFactor
        )
        {
            float valueInRawPixels;
            switch (fromSpace)
            {
                case CommonSpace.PixelTopLeft:
                    valueInRawPixels = value * pixelFactor;
                    break;
                case CommonSpace.DIPTopLeft:
                case CommonSpace.DIP: // A distance in centered DIP is the same as top-left DIP
                    valueInRawPixels = value * dipFactor;
                    break;
                case CommonSpace.Normalized:
                    // In normalized space, Y-axis is [-1, 1], so total height is 2 units.
                    // The value is a fraction of the total normalized height (2).
                    // We treat it as a ratio of the screen height.
                    valueInRawPixels = (value / 2f) * resolution.Y;
                    break;
                default:
                    return value;
            }

            // Now, convert the raw pixel distance to the target space distance.
            switch (toSpace)
            {
                case CommonSpace.PixelTopLeft:
                    return valueInRawPixels / pixelFactor;
                case CommonSpace.DIPTopLeft:
                case CommonSpace.DIP:
                    return valueInRawPixels / dipFactor;
                case CommonSpace.Normalized:
                    return (valueInRawPixels / resolution.Y) * 2f;
                default:
                    return value;
            }
        }
    }
}
