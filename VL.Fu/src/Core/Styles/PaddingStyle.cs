using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga padding of one edge in points: the space between this node's border and its children.
    /// <see cref="YGEdge.All"/>, <see cref="YGEdge.Horizontal"/> and <see cref="YGEdge.Vertical"/> cover several edges,
    /// <see cref="YGEdge.Start"/> and <see cref="YGEdge.End"/> follow the layout direction.
    /// </summary>
    /// <remarks>
    /// Writing an edge clears the more specific edges it covers, so this beats earlier padding on those edges; see <see cref="YogaEdges"/>.
    /// Yoga treats a negative padding as 0.
    /// </remarks>
    public sealed record PaddingStyle(IReadOnlyList<IStyle> Inputs, YGEdge Edge, float Value) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEdges.EnsureSupported(Edge, Value, "Padding");
            YogaEdges.Set(node, &SetPadding, Edge, Value);
        }

        internal static unsafe void SetPadding(YGNode* node, YGEdge edge, float value) => node->SetPadding(edge, value);
    }
}
