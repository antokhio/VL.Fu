using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga min height: the height Yoga never lays this node out below, whatever its height, flex or content asks for.
    /// Points and ratio (stored as Yoga percent of the parent's height) write the same slot, so the later instruction wins.
    /// </summary>
    public sealed record MinHeightStyle(IReadOnlyList<IStyle> Inputs, YGValue MinHeight) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => YogaDimension.SetMinHeight(node, MinHeight);
    }
}
