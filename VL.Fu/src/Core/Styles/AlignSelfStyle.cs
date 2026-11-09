using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga align self: how this node is placed along the cross axis of its line,
    /// beating the parent's <see cref="AlignItemsStyle"/>. Without it, or with <c>Auto</c>, the parent's align items holds.
    /// </summary>
    public sealed record AlignSelfStyle(IReadOnlyList<IStyle> Inputs, YGAlign AlignSelf) : YogaStyle(Inputs)
    {
        /// <summary>
        /// Tells whether <paramref name="align"/> is an align-self value: <c>Auto</c> or an align-items value.
        /// </summary>
        internal static bool IsSupported(YGAlign align) => align == YGAlign.Auto || AlignItemsStyle.IsSupported(align);

        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(AlignSelf, nameof(AlignSelf), IsSupported);
            node->SetAlignSelf(AlignSelf);
        }
    }
}
