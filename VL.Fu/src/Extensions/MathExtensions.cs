using Stride.Core.Mathematics;

namespace VL.Fu.Extensions
{
    public static class MathExtensions
    {
        public static Vector2 SkiaSpaceMul = new Vector2(1.0f, -1.0f);

        public static Vector2 ToSkiaSpace(this Vector2 input, float dip = 1.0f) =>
            input * SkiaSpaceMul;

        public static RectangleF ToNormalizedSpace(this RectangleF pixelRect, Int2 size)
        {
            float aspect = (float)size.X / size.Y;

            float normX = (pixelRect.X / size.X) * 2f * aspect - aspect;
            float normY = (pixelRect.Y / size.Y) * 2f - 1f;
            float normWidth = (pixelRect.Width / size.X) * 2f * aspect;
            float normHeight = (pixelRect.Height / size.Y) * 2f;

            return new RectangleF(normX, normY, normWidth, normHeight);
        }

        public static RectangleF ToCenteredDIPSpace(
            this RectangleF rectangleInPixels,
            Int2 resolution,
            float dipFactor = 100.0f
        )
        {
            // Calculate the total width and height in the new 'DIP' units
            float totalWidthInUnits = resolution.X / dipFactor;
            float totalHeightInUnits = resolution.Y / dipFactor;

            // Calculate the half dimensions in the new 'DIP' units
            float halfWidthInUnits = totalWidthInUnits / 2f;
            float halfHeightInUnits = totalHeightInUnits / 2f;

            // The input rectangle is assumed to be in full DIP pixels (like totalDIPSize),
            // so we must scale its components down to the new 'DIP' units as well.

            float normX = (rectangleInPixels.X / dipFactor) - halfWidthInUnits;
            float normY = (rectangleInPixels.Y / dipFactor) - halfHeightInUnits;

            float normWidth = rectangleInPixels.Width / dipFactor;
            float normHeight = rectangleInPixels.Height / dipFactor;

            return new RectangleF(normX, normY, normWidth, normHeight);
        }

        public static RectangleF ToDIPTopLeftSpace(
            this RectangleF rectangleInPixels,
            float dipFactor = 100.0f
        )
        {
            float scaledX = rectangleInPixels.X / dipFactor;
            float scaledY = rectangleInPixels.Y / dipFactor;
            float scaledWidth = rectangleInPixels.Width / dipFactor;
            float scaledHeight = rectangleInPixels.Height / dipFactor;

            return new RectangleF(scaledX, scaledY, scaledWidth, scaledHeight);
        }

        public static RectangleF ToPixelTopLeftSpace(
            this RectangleF rectangleInPixels,
            float pixelFactor = 100.0f
        )
        {
            float scaledX = rectangleInPixels.X / pixelFactor;
            float scaledY = rectangleInPixels.Y / pixelFactor;
            float scaledWidth = rectangleInPixels.Width / pixelFactor;
            float scaledHeight = rectangleInPixels.Height / pixelFactor;

            return new RectangleF(scaledX, scaledY, scaledWidth, scaledHeight);
        }

        public static Vector2 ToNormalizedSpace(this Vector2 pixelPosition, Vector2 size)
        {
            float aspect = size.X / size.Y;

            float normX = (pixelPosition.X / size.X) * 2f * aspect - aspect;
            float normY = (pixelPosition.Y / size.Y) * 2f - 1f;

            return new Vector2(normX, normY);
        }

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

        public static Vector2 ToDIPTopLeftSpace(this Vector2 pixelPosition, float dipFactor)
        {
            float scaledX = pixelPosition.X / dipFactor;
            float scaledY = pixelPosition.Y / dipFactor;

            return new Vector2(scaledX, scaledY);
        }

        public static Vector2 ToPixelTopLeftSpace(this Vector2 pixelPosition, float pixelFactor)
        {
            float scaledX = pixelPosition.X / pixelFactor;
            float scaledY = pixelPosition.Y / pixelFactor;

            return new Vector2(scaledX, scaledY);
        }
    }
}
