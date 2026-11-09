using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga flex basis: this node's size along its parent's main axis before free space is shared out.
    /// Points, ratio (stored as Yoga percent of the parent's main axis) and auto write the same slot, so the later instruction wins.
    /// Without it, or with auto, the basis is the node's width or height on that axis.
    /// </summary>
    public sealed record FlexBasisStyle(IReadOnlyList<IStyle> Inputs, YGValue FlexBasis) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => YogaDimension.SetFlexBasis(node, FlexBasis);
    }
}
