using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga align items: how the children are placed along the cross axis of their line.
    /// Without it the children stretch across the line. A child's own <see cref="AlignSelfStyle"/> beats it.
    /// </summary>
    public sealed record AlignItemsStyle(IReadOnlyList<IStyle> Inputs, YGAlign AlignItems) : YogaStyle(Inputs)
    {
        /// <summary>
        /// Tells whether <paramref name="align"/> is an align-items value. <c>Auto</c> and the space distributions
        /// only make sense for a single child or for lines, so they are rejected.
        /// </summary>
        internal static bool IsSupported(YGAlign align) =>
            align is YGAlign.FlexStart or YGAlign.Center or YGAlign.FlexEnd or YGAlign.Stretch or YGAlign.Baseline;

        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(AlignItems, nameof(AlignItems), IsSupported);
            node->SetAlignItems(AlignItems);
        }
    }
}
