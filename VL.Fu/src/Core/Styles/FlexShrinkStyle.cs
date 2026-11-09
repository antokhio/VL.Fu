using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga flex shrink: this node's share of the overflow to give up along its parent's main axis.
    /// Fu uses the web defaults, so without it the node shrinks with a share of 1. A root never shrinks.
    /// </summary>
    public sealed record FlexShrinkStyle(IReadOnlyList<IStyle> Inputs, float FlexShrink) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => node->SetFlexShrink(FlexShrink);
    }
}
