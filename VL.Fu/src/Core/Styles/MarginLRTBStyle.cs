using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga margin of the four physical edges in points.
    /// An edge holding NaN is not written, so an earlier setter or the default of 0 holds for it.
    /// </summary>
    /// <remarks>
    /// Each written edge clears the Start and End margin, so it beats earlier margin; see <see cref="YogaEdges"/>.
    /// A negative margin pulls the node over its neighbours.
    /// </remarks>
    public sealed record MarginLRTBStyle(IReadOnlyList<IStyle> Inputs, float Left, float Right, float Top, float Bottom)
        : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEdges.EnsureSupported(Left, Right, Top, Bottom, "Margin");
            YogaEdges.Set(node, &MarginStyle.SetMargin, Left, Right, Top, Bottom);
        }
    }
}
