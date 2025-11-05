using Stride.Core.Mathematics;

namespace VL.Fu.Core.HitTest
{
    /// <summary>
    /// Provides an interface for testing if an object's area is contained within a given rectangle.
    /// </summary>
    public interface IAreaTestProvider
    {
        /// <summary>
        /// Determines whether the object's entire bounds are contained within the specified area.
        /// </summary>
        /// <param name="area">The rectangular area to test against.</param>
        /// <returns>True if the object is fully contained within the area; otherwise, false.</returns>
        bool IsContainedIn(RectangleF area);
    }
}
