using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga margin of one edge in points: the space around this node's border, kept free of its siblings.
    /// <see cref="YGEdge.All"/>, <see cref="YGEdge.Horizontal"/> and <see cref="YGEdge.Vertical"/> cover several edges,
    /// <see cref="YGEdge.Start"/> and <see cref="YGEdge.End"/> follow the layout direction.
    /// </summary>
    /// <remarks>
    /// Writing an edge clears the more specific edges it covers, so this beats earlier margin on those edges; see <see cref="YogaEdges"/>.
    /// A negative margin pulls the node over its neighbours.
    /// </remarks>
    public sealed record MarginStyle(IReadOnlyList<IStyle> Inputs, YGEdge Edge, float Value) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEdges.EnsureSupported(Edge, Value, "Margin");
            YogaEdges.Set(node, &SetMargin, Edge, Value);
        }

        internal static unsafe void SetMargin(YGNode* node, YGEdge edge, float value) => node->SetMargin(edge, value);
    }
}
