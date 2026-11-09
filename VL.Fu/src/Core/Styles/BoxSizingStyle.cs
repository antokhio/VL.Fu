using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga box sizing: what width and height measure.
    /// BorderBox, the default, includes padding and border; ContentBox measures the content only,
    /// so padding and border are added on top.
    /// </summary>
    public sealed record BoxSizingStyle(IReadOnlyList<IStyle> Inputs, YGBoxSizing BoxSizing) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(BoxSizing, nameof(BoxSizing));
            node->SetBoxSizing(BoxSizing);
        }
    }
}
