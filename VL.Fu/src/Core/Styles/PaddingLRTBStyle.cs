using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga padding of the four physical edges in points.
    /// An edge holding NaN is not written, so an earlier setter or the default of 0 holds for it.
    /// </summary>
    /// <remarks>
    /// Each written edge clears the Start and End padding, so it beats earlier padding; see <see cref="YogaEdges"/>.
    /// Yoga treats a negative padding as 0.
    /// </remarks>
    public sealed record PaddingLRTBStyle(IReadOnlyList<IStyle> Inputs, float Left, float Right, float Top, float Bottom)
        : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEdges.EnsureSupported(Left, Right, Top, Bottom, "Padding");
            YogaEdges.Set(node, &PaddingStyle.SetPadding, Left, Right, Top, Bottom);
        }
    }
}
