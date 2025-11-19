using Stride.Core.Mathematics;

namespace VL.Fu.Core.Extensions
{
    public static class SpaceExtensions
    {
        /// <summary>
        /// Converts a vector from pixel coordinates to a normalized, aspect-ratio-corrected space where (0,0) is the center.
        /// </summary>
        /// <param name="pixelPosition">The source position in pixel coordinates.</param>
        /// <param name="clientArea">The total size of the viewport in pixels.</param>
        /// <returns>A new vector in normalized coordinate space.</returns>
        public static Vector2 ToNormalizedSpace(this Vector2 pixelPosition, Vector2 clientArea)
        {
            float aspect = clientArea.X / clientArea.Y;
            Vector2 norm;
            norm.X = (pixelPosition.X / clientArea.X) * 2f - 1f;
            norm.Y = (pixelPosition.Y / clientArea.Y) * 2f - 1f;
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
        /// <param name="clientArea">The total resolution of the viewport in pixels.</param>
        /// <param name="dipFactor">The factor to scale pixels by.</param>
        /// <returns>A new vector in centered DIP space.</returns>
        public static Vector2 ToCenteredDIPSpace(
            this Vector2 pixelPosition,
            Vector2 clientArea,
            float dipFactor
        )
        {
            float halfWidthInUnits = (clientArea.X / dipFactor) / 2f;
            float halfHeightInUnits = (clientArea.Y / dipFactor) / 2f;

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
