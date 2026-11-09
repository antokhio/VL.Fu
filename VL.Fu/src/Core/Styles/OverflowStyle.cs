using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga overflow. For the layout, Scroll lets children grow past the node along its main axis
    /// instead of being shrunk to fit; Visible, the default, and Hidden lay out alike.
    /// </summary>
    /// <remarks>
    /// Rendering doesn't clip yet, so Hidden and Scroll only change the layout for now.
    /// </remarks>
    public sealed record OverflowStyle(IReadOnlyList<IStyle> Inputs, YGOverflow Overflow) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(Overflow, nameof(Overflow));
            node->SetOverflow(Overflow);
        }
    }
}
