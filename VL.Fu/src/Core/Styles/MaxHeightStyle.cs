using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga max height: the height Yoga never lays this node out above, whatever its height, flex or content asks for.
    /// Points and ratio (stored as Yoga percent of the parent's height) write the same slot, so the later instruction wins.
    /// A min height beats it when the two conflict.
    /// </summary>
    public sealed record MaxHeightStyle(IReadOnlyList<IStyle> Inputs, YGValue MaxHeight) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => YogaDimension.SetMaxHeight(node, MaxHeight);
    }
}
