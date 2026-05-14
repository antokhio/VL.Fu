using Stride.Core.Mathematics;
using VL.Core;
using YogaSharp;

namespace VL.Fu.Core
{
    public static class LayoutableExtensions
    {
        public static void CalculateLayout(
            this ILayoutable layoutable,
            Optional<RectangleF> ownerBounds,
            Optional<YGDirection> ownerDirection
        )
        {
            var width = ownerBounds.HasValue ? ownerBounds.Value.Width : float.NaN;
            var height = ownerBounds.HasValue ? ownerBounds.Value.Height : float.NaN;
            var direction = ownerDirection.ValueOrDefault(YGDirection.Inherit);

            unsafe
            {
                layoutable.Handle->CalculateLayout(width, height, direction);
            }

            var args = new LayoutableCalculateLayoutArgs(ownerBounds.ToNullable(), direction);

            layoutable.ApplyLayout(args, ownerBounds.ToNullable());
        }

        public static void MarkDirty(this ILayoutable layoutable)
        {
            unsafe
            {
                layoutable.Handle->MarkDirty();
            }
        }

        public static bool IsDirty(this ILayoutable layoutable)
        {
            unsafe
            {
                return layoutable.Handle->IsDirty();
            }
        }

        public static bool HasNewLayout(this ILayoutable layoutable)
        {
            unsafe
            {
                return layoutable.Handle->GetHasNewLayout();
            }
        }

        public static void SetHasNewLayout(this ILayoutable layoutable, bool hasNewLayout)
        {
            unsafe
            {
                layoutable.Handle->SetHasNewLayout(hasNewLayout);
            }
        }
    }
}
