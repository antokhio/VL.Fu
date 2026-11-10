using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga overflow. Hidden and Scroll clip the node's children to its padding box, inside the border;
    /// the node's own layer is not clipped. Visible, the default, clips nothing.
    /// </summary>
    /// <remarks>
    /// For the layout, Scroll lets children grow past the node along its main axis instead of being shrunk to fit;
    /// Visible and Hidden lay out alike. Scrolling the content is done with a transformation on the content node.
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
