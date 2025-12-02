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
        /// Converts a Circle from a specified source space to the viewport's current space.
        /// </summary>
        /// <param name="viewport">The context viewport.</param>
        /// <param name="circle">The circle to convert.</param>
        /// <param name="fromSpace">The coordinate space of the input circle.</param>
        /// <returns>The converted circle in the viewport's space.</returns>
        public static Circle ConvertSpace(
            this FuViewport viewport,
            Circle circle,
            CommonSpace fromSpace
        )
        {
            if (fromSpace == viewport.Space)
            {
                return circle;
            }

            var newCenter = viewport.ConvertPosition(circle.Center, fromSpace);
            // The radius is a distance. We use the float conversion, which correctly scales based on the height of the coordinate spaces.
            var newRadius = viewport.ConvertSpace(circle.Radius, fromSpace);

            return new Circle(newCenter, newRadius);
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
        /// This calculates the Full Coordinate System Bounds, not the Visible Bounds.
        /// </summary>
        private static RectangleF GetLogicalBounds(CommonSpace space, FuViewport viewport)
        {
            float w,
                h;
            bool centered = false;

            // Avoid divide by zero
            float clientX = Math.Max(viewport.ClientArea.X, 1f);
            float clientY = Math.Max(viewport.ClientArea.Y, 1f);
            float dipFactor = Math.Max(viewport.DIPFactor, 1f);
            float pixelFactor = Math.Max(viewport.PixelFactor, 1f);

            switch (space)
            {
                case CommonSpace.Normalized:
                    float aspect = clientX / clientY;
                    if (aspect > 1)
                    {
                        w = 2 * aspect;
                        h = 2;
                    }
                    else
                    {
                        w = 2;
                        h = 2 / aspect;
                    }
                    centered = true;
                    break;

                case CommonSpace.DIP:
                    w = clientX / dipFactor;
                    h = clientY / dipFactor;
                    centered = true;
                    break;

                case CommonSpace.DIPTopLeft:
                    w = clientX / dipFactor;
                    h = clientY / dipFactor;
                    centered = false;
                    break;

                case CommonSpace.PixelTopLeft:
                    w = clientX / pixelFactor;
                    h = clientY / pixelFactor;
                    centered = false;
                    break;

                default:
                    // Fallback to the current viewport's coordinate system if matched,
                    // otherwise assume it maps to current viewport view.
                    // Note: ViewportBounds represents the VISIBLE area, which may be zoomed.
                    // If we are dealing with an unknown space, we might not have a choice.
                    return viewport.ViewportBounds;
            }

            if (centered)
                return new RectangleF(-w / 2f, -h / 2f, w, h);
            else
                return new RectangleF(0f, 0f, w, h);
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
