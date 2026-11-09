using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga max width: the width Yoga never lays this node out above, whatever its width, flex or content asks for.
    /// Points and ratio (stored as Yoga percent of the parent's width) write the same slot, so the later instruction wins.
    /// A min width beats it when the two conflict.
    /// </summary>
    public sealed record MaxWidthStyle(IReadOnlyList<IStyle> Inputs, YGValue MaxWidth) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => YogaDimension.SetMaxWidth(node, MaxWidth);
    }
}
