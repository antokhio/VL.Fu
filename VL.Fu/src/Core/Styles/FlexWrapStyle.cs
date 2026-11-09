using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga flex wrap: whether children that don't fit the main axis move to a new line.
    /// Without it the children stay on one line.
    /// </summary>
    public sealed record FlexWrapStyle(IReadOnlyList<IStyle> Inputs, YGWrap FlexWrap) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(FlexWrap, nameof(FlexWrap));
            node->SetFlexWrap(FlexWrap);
        }
    }
}
