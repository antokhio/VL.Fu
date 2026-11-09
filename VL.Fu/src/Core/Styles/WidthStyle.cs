using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga width. Points, ratio (stored as Yoga percent) and auto write the same slot,
    /// so the later instruction wins, also over the width of a <see cref="SizeStyle"/>.
    /// </summary>
    public sealed record WidthStyle(IReadOnlyList<IStyle> Inputs, YGValue Width) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => YogaDimension.SetWidth(node, Width);
    }
}
