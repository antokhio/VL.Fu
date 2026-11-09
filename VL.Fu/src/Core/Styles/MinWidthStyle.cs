using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga min width: the width Yoga never lays this node out below, whatever its width, flex or content asks for.
    /// Points and ratio (stored as Yoga percent of the parent's width) write the same slot, so the later instruction wins.
    /// </summary>
    public sealed record MinWidthStyle(IReadOnlyList<IStyle> Inputs, YGValue MinWidth) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => YogaDimension.SetMinWidth(node, MinWidth);
    }
}
