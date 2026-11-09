using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga position offsets in points: X writes the left slot and Y the top slot.
    /// NaN marks an axis as not set, so an earlier setter or the default holds for it.
    /// </summary>
    /// <remarks>
    /// How the offsets apply depends on <see cref="PositionTypeStyle"/>: relative to the node's place in the flow,
    /// or to its parent's padding box when absolute. Writes go through <see cref="YogaEdges"/>,
    /// so X also clears an earlier Start or End offset and the later instruction wins.
    /// </remarks>
    public sealed record PositionStyle(IReadOnlyList<IStyle> Inputs, float X, float Y) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            if (float.IsInfinity(X) || float.IsInfinity(Y))
                throw new ArgumentOutOfRangeException(nameof(PositionStyle), "Position must be finite, or NaN for not set.");

            if (!float.IsNaN(X))
                YogaEdges.Set(node, &SetPosition, YGEdge.Left, X);
            if (!float.IsNaN(Y))
                YogaEdges.Set(node, &SetPosition, YGEdge.Top, Y);
        }

        internal static unsafe void SetPosition(YGNode* node, YGEdge edge, float value) => node->SetPosition(edge, value);
    }
}
