using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga display. Flex, the default, lays the node out.
    /// None removes the node and its subtree from the layout; Fu also skips them when rendering.
    /// Contents removes only the node's own box, so its children are laid out as if they were its parent's.
    /// </summary>
    public sealed record DisplayStyle(IReadOnlyList<IStyle> Inputs, YGDisplay Display) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(Display, nameof(Display));
            node->SetDisplay(Display);
        }
    }
}
