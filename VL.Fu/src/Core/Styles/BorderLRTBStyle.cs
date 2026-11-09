using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga border width of the four physical edges in points. Nothing is drawn by it.
    /// An edge holding NaN is not written, so an earlier setter or the default of 0 holds for it.
    /// </summary>
    /// <remarks>
    /// Each written edge clears the Start and End border, so it beats earlier borders; see <see cref="YogaEdges"/>.
    /// Yoga treats a negative border as 0.
    /// </remarks>
    public sealed record BorderLRTBStyle(IReadOnlyList<IStyle> Inputs, float Left, float Right, float Top, float Bottom)
        : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEdges.EnsureSupported(Left, Right, Top, Bottom, "Border");
            YogaEdges.Set(node, &BorderStyle.SetBorder, Left, Right, Top, Bottom);
        }
    }
}
