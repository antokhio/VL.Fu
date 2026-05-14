using Stride.Core.Mathematics;
using YogaSharp;

namespace VL.Fu.Core
{
    /// <summary>
    /// Arguments used in layout calculation, passed to breakpoints
    /// </summary>
    public record struct LayoutableCalculateLayoutArgs(
        RectangleF? OwnerBounds,
        YGDirection OwnerDirection = YGDirection.Inherit
    ) { }
}
