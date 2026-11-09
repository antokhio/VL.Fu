using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga height. Points, ratio (stored as Yoga percent) and auto write the same slot,
    /// so the later instruction wins, also over the height of a <see cref="SizeStyle"/>.
    /// </summary>
    public sealed record HeightStyle(IReadOnlyList<IStyle> Inputs, YGValue Height)
        : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => YogaDimension.SetHeight(node, Height);
    }
}
