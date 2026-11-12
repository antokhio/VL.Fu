using System;
using SkiaSharp;
using Stride.Core.Mathematics;

namespace VL.Fu.Core.Interaction.Recognizers
{
    /// <summary>
    /// The few math steps every recognizer shares: mapping a window position through a frozen matrix,
    /// distances in DIP and angle wrapping.
    /// </summary>
    internal static class GestureMath
    {
        /// <summary>
        /// <paramref name="positionInWindow"/> mapped through <paramref name="windowToSpace"/>.
        /// </summary>
        public static Vector2 Map(in SKMatrix windowToSpace, Vector2 positionInWindow)
        {
            var p = windowToSpace.MapPoint(positionInWindow.X, positionInWindow.Y);
            return new Vector2(p.X, p.Y);
        }

        /// <summary>
        /// <paramref name="delta"/> expressed in another space: <c>map · delta · inverse</c>, where <paramref name="map"/> maps
        /// the space <paramref name="delta"/> works in into the other one and <paramref name="inverse"/> maps back.
        /// </summary>
        public static SKMatrix Conjugate(in SKMatrix delta, in SKMatrix map, in SKMatrix inverse) =>
            map.PreConcat(delta).PreConcat(inverse);

        /// <summary>
        /// The distance between two window positions in DIP,
        /// </summary>
        public static float DistanceDip(Vector2 fromInWindow, Vector2 toInWindow, float scaling) =>
            (toInWindow - fromInWindow).Length() / scaling;

        /// <summary>
        /// <paramref name="radians"/> wrapped into (−π, π].
        /// </summary>
        public static float Wrap(float radians)
        {
            var wrapped = MathF.IEEERemainder(radians, MathF.Tau);
            return wrapped <= -MathF.PI ? wrapped + MathF.Tau : wrapped;
        }

        /// <summary>
        /// True for a finite threshold of zero or more, the only values a recognizer setting accepts.
        /// </summary>
        public static bool IsValidThreshold(float thresholdDip) => float.IsFinite(thresholdDip) && thresholdDip >= 0f;
    }
}
