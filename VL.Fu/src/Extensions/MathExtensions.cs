using Stride.Core.Mathematics;

namespace VL.Fu.Extensions
{
    public static class MathExtensions
    {
        public static Vector2 ToVector(this Int2 value) => new Vector2(value.X, value.Y);

        public static Int2 ToInt(this Vector2 value) =>
            new Int2((int)Math.Round(value.X), (int)Math.Round(value.Y));
    }
}
