using System;
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Core.Extensions;

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
        /// <paramref name="matrix"/> changed by <paramref name="deltaInParent"/>, which works in the parent's space,
        /// compensated for the pivot the transformation applies about (v1 §7.3): <c>P⁻¹ · D · P · M</c>.
        /// Z stays as it was.
        /// </summary>
        /// <param name="matrix">A node's transformation, applied about its pivot.</param>
        /// <param name="pivotInParent">Where that pivot is in the parent's space.</param>
        /// <param name="deltaInParent">The change, in the parent's space.</param>
        public static Matrix ComposeAboutPivot(in Matrix matrix, Vector2 pivotInParent, in SKMatrix deltaInParent)
        {
            var aboutPivot = SKMatrix
                .CreateTranslation(-pivotInParent.X, -pivotInParent.Y)
                .PreConcat(deltaInParent)
                .PreConcat(SKMatrix.CreateTranslation(pivotInParent.X, pivotInParent.Y));

            // Stride applies the left matrix first: the transformation, then the change.
            return matrix * aboutPivot.ToMatrix();
        }

        /// <summary>
        /// How much <paramref name="matrix"/> scales: the length of its X axis. For a view that only scales and translates
        /// it is <c>M11</c>, and it stays right when the matrix also turns.
        /// </summary>
        public static float ScaleOf(in Matrix matrix) => new Vector2(matrix.M11, matrix.M12).Length();

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
