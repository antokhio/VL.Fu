using Stride.Core.Mathematics;
using VL.Fu.Core.Input;
using VL.Lib.Mathematics;
using VL.Skia;

namespace VL.Fu.Core.Extensions
{
    /// <summary>
    /// Provides extension methods for coordinate space conversions using an FuViewport as the context.
    /// </summary>
    public static class ViewportExtensions
    {
        #region Public API

        /// <summary>
        /// Converts a Vector2 representing a position from a specified source space to the viewport's current space.
        /// </summary>
        /// <param name="viewport">The context viewport.</param>
        /// <param name="position">The position vector to convert.</param>
        /// <param name="fromSpace">The coordinate space of the input vector.</param>
        /// <returns>The converted position vector in the viewport's space.</returns>
        public static Vector2 ConvertPosition(
            this FuViewport viewport,
            Vector2 position,
            CommonSpace fromSpace
        )
        {
            if (fromSpace == viewport.Space)
            {
                return position;
            }

            var fromBounds = GetLogicalBounds(fromSpace, viewport);
            var toBounds = GetLogicalBounds(viewport.Space, viewport);

            // Convert from the source space to a common "Normalized" [0, 1] space
            var normalizedT = InverseLerp(position, fromBounds);

            // Then, convert from that intermediate space to the target space.
            return Lerp(normalizedT, toBounds);
        }

        /// <summary>
        /// Converts a Vector2 representing a direction or size from a specified source space to the viewport's current space.
        /// This conversion only accounts for scaling, not translation.
        /// </summary>
        /// <param name="viewport">The context viewport.</param>
        /// <param name="directionOrSize">The direction or size vector to convert.</param>
        /// <param name="fromSpace">The coordinate space of the input vector.</param>
        /// <returns>The converted direction or size vector in the viewport's space.</returns>
        public static Vector2 ConvertSize(
            this FuViewport viewport,
            Vector2 directionOrSize,
            CommonSpace fromSpace
        )
        {
            if (fromSpace == viewport.Space)
            {
                return directionOrSize;
            }

            var fromBounds = GetLogicalBounds(fromSpace, viewport);
            var toBounds = GetLogicalBounds(viewport.Space, viewport);

            // Calculate the vector's ratio relative to the source space's dimensions
            var ratio = new Vector2(
                MathUtil.NearEqual(fromBounds.Width, 0) ? 0 : directionOrSize.X / fromBounds.Width,
                MathUtil.NearEqual(fromBounds.Height, 0) ? 0 : directionOrSize.Y / fromBounds.Height
            );

            // Apply that same ratio to the target space's dimensions
            return new Vector2(ratio.X * toBounds.Width, ratio.Y * toBounds.Height);
        }

        /// <summary>
        /// Converts a RectangleF from a specified source space to the viewport's current space.
        /// </summary>
        /// <param name="viewport">The context viewport.</param>
        /// <param name="value">The rectangle to convert.</param>
        /// <param name="fromSpace">The coordinate space of the input rectangle.</param>
        /// <returns>The converted rectangle in the viewport's space.</returns>
        public static RectangleF ConvertSpace(
            this FuViewport viewport,
            RectangleF value,
            CommonSpace fromSpace
        )
        {
            if (fromSpace == viewport.Space)
            {
                return value;
            }

            // A rectangle is defined by two points. The most robust way to convert it is to convert both points.
            var p1 = viewport.ConvertPosition(value.TopLeft, fromSpace);
            var p2 = viewport.ConvertPosition(value.BottomRight, fromSpace);

            RectangleNodes.JoinPoints(ref p1, ref p2, out var result);
            return result;
        }

        /// <summary>
        /// Converts a scalar value (representing a distance) from a specified source space to the viewport's current space.
        /// </summary>
        /// <param name="viewport">The context viewport.</param>
        /// <param name="value">The scalar distance to convert.</param>
        /// <param name="fromSpace">The coordinate space of the input scalar.</param>
        /// <returns>The converted scalar in the viewport's space.</returns>
        public static float ConvertSpace(
            this FuViewport viewport,
            float value,
            CommonSpace fromSpace
        )
        {
            if (fromSpace == viewport.Space)
            {
                return value;
            }

            var fromBounds = GetLogicalBounds(fromSpace, viewport);
            var toBounds = GetLogicalBounds(viewport.Space, viewport);

            // Calculate the scalar's ratio relative to the source space's height
            float ratio = MathUtil.NearEqual(fromBounds.Height, 0) ? 0 : value / fromBounds.Height;

            // Apply that same ratio to the target space's height
            return ratio * toBounds.Height;
        }

        #endregion

        #region Private Conversion Helpers

        /// <summary>
        /// Determines the logical bounding box for a given space, using the viewport as context.
        /// </summary>
        private static RectangleF GetLogicalBounds(CommonSpace space, FuViewport viewport)
        {
            // For all spaces except Normalized, their logical bounds are simply the viewport's current bounds in that space.
            if (space != CommonSpace.Normalized)
            {
                return viewport.ViewportBounds;
            }

            // For Normalized space, the bounds are [-1, 1] on the shorter axis, and aspect-corrected on the longer one.
            var bounds = viewport.ViewportBounds;
            var aspectRatio = MathUtil.NearEqual(bounds.Height, 0)
                ? 1
                : bounds.Width / bounds.Height;

            if (aspectRatio > 1) // Wider than tall
            {
                return new RectangleF(-aspectRatio, -1, 2 * aspectRatio, 2);
            }

            // Taller than wide
            return new RectangleF(-1, -1 / aspectRatio, 2, 2 / aspectRatio);
        }

        /// <summary>
        /// Inverse linear interpolation. Calculates the normalized position of a value within a bounding box.
        /// </summary>
        /// <returns>A Vector2 with components in the range [0, 1].</returns>
        private static Vector2 InverseLerp(Vector2 value, RectangleF bounds)
        {
            // Avoid division by zero for zero-sized rectangles
            var w = MathUtil.NearEqual(bounds.Width, 0)
                ? 0
                : (value.X - bounds.Left) / bounds.Width;
            var h = MathUtil.NearEqual(bounds.Height, 0)
                ? 0
                : (value.Y - bounds.Top) / bounds.Height;
            return new Vector2(w, h);
        }

        /// <summary>
        /// Linear interpolation. Calculates a value inside a bounding box from a normalized position.
        /// </summary>
        /// <param name="t">A Vector2 with components in the range [0, 1].</param>
        /// <param name="bounds">The target bounding box.</param>
        /// <returns>The denormalized position inside the bounds.</returns>
        private static Vector2 Lerp(Vector2 t, RectangleF bounds)
        {
            var x = bounds.Left + t.X * bounds.Width;
            var y = bounds.Top + t.Y * bounds.Height;
            return new Vector2(x, y);
        }

        #endregion
    }
}
