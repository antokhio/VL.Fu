using Stride.Core.Mathematics;

namespace VL.Fu.Core.Selection
{
    /// <summary>
    /// Defines the geometry of a selection area (e.g. Marquee, Lasso, Brush).
    /// </summary>
    public interface ISelectionShape
    {
        /// <summary>
        /// Determines if the provided bounds are strictly contained within this shape.
        /// </summary>
        bool Contains(RectangleF bounds);

        /// <summary>
        /// Determines if the provided bounds intersect with this shape.
        /// (Optional for strict containment, but useful for "crossing" selection modes).
        /// </summary>
        bool Intersects(RectangleF bounds);
    }
}
