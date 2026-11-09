using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga width and height in one instruction. It writes the same slots as
    /// <see cref="WidthStyle"/> and <see cref="HeightStyle"/>, so whichever runs later wins per axis.
    /// </summary>
    /// <remarks>
    /// Both units are checked before anything is written, so a size with an unsupported unit
    /// is skipped as a whole instead of setting only one axis.
    /// </remarks>
    public sealed record SizeStyle(IReadOnlyList<Style> Inputs, YGValue Width, YGValue Height)
        : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaDimension.EnsureSupported(Width, nameof(Width));
            YogaDimension.EnsureSupported(Height, nameof(Height));

            YogaDimension.SetWidth(node, Width);
            YogaDimension.SetHeight(node, Height);
        }
    }
}