using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga justify content: how the children and the free space are placed along the main axis.
    /// Without it the children start at the beginning of the main axis.
    /// </summary>
    public sealed record JustifyContentStyle(IReadOnlyList<IStyle> Inputs, YGJustify JustifyContent) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(JustifyContent, nameof(JustifyContent));
            node->SetJustifyContent(JustifyContent);
        }
    }
}
