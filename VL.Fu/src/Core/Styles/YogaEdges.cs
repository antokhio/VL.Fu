using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Writes padding, margin or border edges so that the later instruction wins.
    /// Shared by the edge records, so they treat unset and invalid edges the same way.
    /// </summary>
    /// <remarks>
    /// Yoga resolves an edge by specificity, not by order: Start/End beat Left/Right, which beat Horizontal, which beats All
    /// (Top/Bottom beat Vertical, which beats All). Writing a slot therefore also clears the more specific slots it covers,
    /// so an earlier per-edge value can't beat a later, broader one:
    /// All clears every other slot, Horizontal clears Left, Right, Start and End, Vertical clears Top and Bottom,
    /// and Left or Right clears Start and End. Which of Start and End lands on the left depends on the layout direction,
    /// unknown while styling, so both are cleared, also dropping one of them from the opposite side.
    /// </remarks>
    internal static unsafe class YogaEdges
    {
        private static readonly YGEdge[] _coveredByAll =
        [
            YGEdge.Left,
            YGEdge.Right,
            YGEdge.Top,
            YGEdge.Bottom,
            YGEdge.Start,
            YGEdge.End,
            YGEdge.Horizontal,
            YGEdge.Vertical,
        ];
        private static readonly YGEdge[] _coveredByHorizontal = [YGEdge.Left, YGEdge.Right, YGEdge.Start, YGEdge.End];
        private static readonly YGEdge[] _coveredByVertical = [YGEdge.Top, YGEdge.Bottom];
        private static readonly YGEdge[] _coveredByLeftOrRight = [YGEdge.Start, YGEdge.End];

        /// <summary>
        /// Throws when <paramref name="edge"/> is no Yoga edge or <paramref name="value"/> is not finite.
        /// </summary>
        public static void EnsureSupported(YGEdge edge, float value, string name)
        {
            YogaEnums.EnsureSupported(edge, "Edge");
            if (!float.IsFinite(value))
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite.");
        }

        /// <summary>
        /// Throws when an edge holds infinity, so a record can check all edges before writing any of them.
        /// NaN is fine; it means the edge is not written.
        /// </summary>
        public static void EnsureSupported(float left, float right, float top, float bottom, string name)
        {
            if (float.IsInfinity(left) || float.IsInfinity(right) || float.IsInfinity(top) || float.IsInfinity(bottom))
                throw new ArgumentOutOfRangeException(name, $"{name} edges must be finite, or NaN for not set.");
        }

        /// <summary>
        /// Clears the slots more specific than <paramref name="edge"/> that it covers, then writes <paramref name="value"/> to it
        /// through <paramref name="set"/>. Yoga reads a NaN slot as unset.
        /// </summary>
        public static void Set(YGNode* node, delegate*<YGNode*, YGEdge, float, void> set, YGEdge edge, float value)
        {
            foreach (var covered in Covered(edge))
                set(node, covered, float.NaN);

            set(node, edge, value);
        }

        /// <summary>
        /// Writes every physical edge that is not NaN, each clearing the slots it covers like the single-edge overload.
        /// </summary>
        public static void Set(
            YGNode* node,
            delegate*<YGNode*, YGEdge, float, void> set,
            float left,
            float right,
            float top,
            float bottom
        )
        {
            if (!float.IsNaN(left))
                Set(node, set, YGEdge.Left, left);
            if (!float.IsNaN(right))
                Set(node, set, YGEdge.Right, right);
            if (!float.IsNaN(top))
                Set(node, set, YGEdge.Top, top);
            if (!float.IsNaN(bottom))
                Set(node, set, YGEdge.Bottom, bottom);
        }

        private static ReadOnlySpan<YGEdge> Covered(YGEdge edge) =>
            edge switch
            {
                YGEdge.All => _coveredByAll,
                YGEdge.Horizontal => _coveredByHorizontal,
                YGEdge.Vertical => _coveredByVertical,
                YGEdge.Left or YGEdge.Right => _coveredByLeftOrRight,
                _ => [],
            };
    }
}
