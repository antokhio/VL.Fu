using Stride.Core.Mathematics;

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
            float aspect = (float)size.X / size.Y;

            float normX = (pixelRect.X / size.X) * 2f * aspect - aspect;
            float normY = (pixelRect.Y / size.Y) * 2f - 1f;
            float normWidth = (pixelRect.Width / size.X) * 2f * aspect;
            float normHeight = (pixelRect.Height / size.Y) * 2f;

            return new RectangleF(normX, normY, normWidth, normHeight);
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

            float normX = (pixelPosition.X / size.X) * 2f * aspect - aspect;
            float normY = (pixelPosition.Y / size.Y) * 2f - 1f;

            return new Vector2(normX, normY);
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
            Vector2 resolution,
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
    }
}
