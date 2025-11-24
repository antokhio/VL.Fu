using VL.Fu.Core.Input;
using VL.Lib.Mathematics;

namespace VL.Fu.Core.HitTest
{
    /// <summary>
    /// Default strategy: Hit test strictly against the node's bounding box.
    /// </summary>
    public record HitTestBounds : IHitTest
    {
        /// <summary>
        /// Shared singleton instance to avoid allocations.
        /// </summary>
        public static readonly HitTestBounds Instance = new HitTestBounds();

        public bool HitTest(IFuNode node, FuPointer pointer)
        {
            var bounds = node.Bounds;

            if (!bounds.HasValue)
                return false;

            var rect = bounds.Value;
            var pos = pointer.Position;

            Collision2D.RectContainsPoint(ref rect, ref pos, out var contains);

            return contains;
        }
    }
}
