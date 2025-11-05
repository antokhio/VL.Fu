using Stride.Core.Mathematics;
using VL.Fu.Core.Input;
using VL.Lib.Mathematics;

namespace VL.Fu.Core.HitTest
{
    /// <summary>
    /// Provides static helper methods for common hit-testing and area-testing logic,
    /// using the standard VL.CoreLib Collision2D helpers.
    /// </summary>
    public static class HitTestHelper
    {
        /// <summary>
        /// A default hit-test implementation that checks if a pointer's position is contained within a given bounding box.
        /// </summary>
        /// <param name="bounds">The bounds to test against.</param>
        /// <param name="pointer">The pointer to test.</param>
        /// <returns>True if the pointer is inside the bounds; otherwise, false.</returns>
        public static bool RectangleHitTest(RectangleF? bounds, FuPointer pointer)
        {
            if (!bounds.HasValue)
                return false;

            var rect = bounds.Value;
            var point = pointer.Position;
            Collision2D.RectContainsPoint(ref rect, ref point, out bool result);
            return result;
        }

        /// <summary>
        /// A default area-test implementation that checks if a given bounding box is fully contained within a specified area.
        /// </summary>
        /// <param name="bounds">The bounds to test for containment.</param>
        /// <param name="area">The containing area.</param>
        /// <returns>True if the bounds are fully contained within the area; otherwise, false.</returns>
        public static bool RectangleContainedInRectangle(RectangleF? bounds, RectangleF area)
        {
            if (!bounds.HasValue)
                return false;

            var rectToTest = bounds.Value;
            var containingArea = area;
            Collision2D.RectContainsRect(ref containingArea, ref rectToTest, out bool result);
            return result;
        }
    }
}
