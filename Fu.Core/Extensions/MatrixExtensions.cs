using System.Runtime.CompilerServices;
using SkiaSharp;
using Stride.Core.Mathematics;

namespace Fu.Core.Extensions
{
    /// <summary>
    /// Converts between the Stride <see cref="Matrix"/> that VL users patch with and the <see cref="SKMatrix"/> Skia renders with.
    /// Rectangles are already covered by <c>VL.Skia.Conversions</c>.
    /// </summary>
    /// <remarks>
    /// Stride multiplies row vectors (<c>p * M</c>, translation in M41/M42), Skia column vectors (<c>M * p</c>),
    /// so one is the transpose of the other restricted to X, Y and W.
    /// Both apply the same transformation to a point, and Stride's <c>a * b</c> equals Skia's <c>a.PostConcat(b)</c>:
    /// first <c>a</c>, then <c>b</c>.
    /// </remarks>
    internal static class MatrixExtensions
    {
        /// <summary>
        /// Takes the 2D part of <paramref name="matrix"/>, including perspective.
        /// Everything that reads or writes Z is dropped, since a Skia canvas has none.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SKMatrix ToSKMatrix(this in Matrix matrix) =>
            new(
                matrix.M11, matrix.M21, matrix.M41,
                matrix.M12, matrix.M22, matrix.M42,
                matrix.M14, matrix.M24, matrix.M44
            );

        /// <summary>
        /// Builds a Stride matrix that transforms X and Y like <paramref name="matrix"/> and leaves Z unchanged.
        /// Converting back with <see cref="ToSKMatrix"/> gives the same <see cref="SKMatrix"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix ToMatrix(this in SKMatrix matrix) =>
            new(
                matrix.ScaleX, matrix.SkewY, 0, matrix.Persp0,
                matrix.SkewX, matrix.ScaleY, 0, matrix.Persp1,
                0, 0, 1, 0,
                matrix.TransX, matrix.TransY, 0, matrix.Persp2
            );
    }
}
