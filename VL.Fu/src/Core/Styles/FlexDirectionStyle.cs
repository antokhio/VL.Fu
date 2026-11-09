using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga flex direction: the main axis the children are laid out along.
    /// Fu uses the web defaults, so without it the children are laid out in a row.
    /// </summary>
    public sealed record FlexDirectionStyle(IReadOnlyList<IStyle> Inputs, YGFlexDirection FlexDirection) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(FlexDirection, nameof(FlexDirection));
            node->SetFlexDirection(FlexDirection);
        }
    }
}
