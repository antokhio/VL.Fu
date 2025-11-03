using Stride.Core.Mathematics;
using VL.Lib.Mathematics;

namespace VL.Fu.Helpers
{
    public static class AreaTestHelper
    {
        /// <summary>
        /// Default area test logic. Checks if the node's bounds are fully contained within the given area.
        /// </summary>
        public static bool IsContainedIn(IFuNode node, RectangleF area)
        {
            var nodeBounds = node.Bounds;
            if (!nodeBounds.HasValue)
            {
                return false;
            }

            var bounds = nodeBounds.Value;
            // Use RectangleF.Contains() which checks for full containment of another rectangle.
            Collision2D.RectContainsRect(ref area, ref bounds, out var hit);

            return hit;
        }
    }
}
