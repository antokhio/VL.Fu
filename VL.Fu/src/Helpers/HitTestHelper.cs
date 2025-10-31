using VL.Fu.Core;
using VL.Lib.Mathematics;

namespace VL.Fu.Helpers
{
    public class HitTestHelper
    {
        public static bool HitTestBounds(IFuNode node, FuCursor cursor)
        {
            if (node.Bounds.HasValue)
            {
                var rect = node.Bounds.Value;
                var point = cursor.Position;
                Collision2D.RectContainsPoint(ref rect, ref point, out var result);

                return result;
            }

            return false;
        }
    }
}
