using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga border width of one edge in points: layout space between the margin and the padding. Nothing is drawn by it.
    /// <see cref="YGEdge.All"/>, <see cref="YGEdge.Horizontal"/> and <see cref="YGEdge.Vertical"/> cover several edges,
    /// <see cref="YGEdge.Start"/> and <see cref="YGEdge.End"/> follow the layout direction.
    /// </summary>
    /// <remarks>
    /// Writing an edge clears the more specific edges it covers, so this beats earlier borders on those edges; see <see cref="YogaEdges"/>.
    /// Yoga treats a negative border as 0.
    /// </remarks>
    public sealed record BorderStyle(IReadOnlyList<IStyle> Inputs, YGEdge Edge, float Value) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEdges.EnsureSupported(Edge, Value, "Border");
            YogaEdges.Set(node, &SetBorder, Edge, Value);
        }

        internal static unsafe void SetBorder(YGNode* node, YGEdge edge, float value) => node->SetBorder(edge, value);
    }
}
