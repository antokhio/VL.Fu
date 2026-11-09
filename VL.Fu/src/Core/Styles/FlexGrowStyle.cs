using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga flex grow: this node's share of the free space along its parent's main axis.
    /// Without it the node doesn't grow. A root never grows, since it has no parent to share space with.
    /// </summary>
    public sealed record FlexGrowStyle(IReadOnlyList<IStyle> Inputs, float FlexGrow) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => node->SetFlexGrow(FlexGrow);
    }
}
