using Stride.Core.Mathematics;

namespace VL.Fu.Extensions
{
    public static class MathExtensions
    {
        public static Vector2 SkiaSpaceMul = new Vector2(1.0f, -1.0f);

        public static Vector2 ToSkiaSpace(this Vector2 input) => input * SkiaSpaceMul;
    }
}
