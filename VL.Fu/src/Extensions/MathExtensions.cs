using Stride.Core.Mathematics;

namespace VL.Fu.Extensions
{
    public static class MathExtensions
    {
        public static Vector2 SkiaSpaceMul = new Vector2(1.0f, -1.0f);

        public static Vector2 ToSkiaSpace(this Vector2 input) => input * SkiaSpaceMul;

        public static RectangleF ToNormalizedSpace(this RectangleF pixelRect, Int2 size)
        {
            float aspect = (float)size.X / size.Y;

            float normX = (pixelRect.X / size.X) * 2f * aspect - aspect;
            float normY = (pixelRect.Y / size.Y) * 2f - 1f;
            float normWidth = (pixelRect.Width / size.X) * 2f * aspect;
            float normHeight = (pixelRect.Height / size.Y) * 2f;

            return new RectangleF(normX, normY, normWidth, normHeight);
        }
    }
}
