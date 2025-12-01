using Stride.Core.Mathematics;
using VL.Lib.Mathematics;

namespace VL.Fu.Core.Selection
{
    public readonly struct MarqueeShape : ISelectionShape
    {
        public readonly RectangleF Rect;

        public MarqueeShape(Vector2 start, Vector2 end)
        {
            // Efficiently create rect from diagonal points
            RectangleNodes.JoinPoints(ref start, ref end, out Rect);
        }

        public bool Contains(RectangleF bounds)
        {
            var r = Rect;
            Collision2D.RectContainsRect(ref r, ref bounds, out var result);
            return result;
        }

        public bool Intersects(RectangleF bounds)
        {
            var r = Rect;
            Collision2D.RectIntersectsRect(ref r, ref bounds, out var result);
            return result;
        }
    }
}
