using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga align content: how the lines of a wrapping node are placed along the cross axis.
    /// It has no effect on a node whose children fit one line. Fu uses the web defaults, so without it the lines stretch.
    /// </summary>
    public sealed record AlignContentStyle(IReadOnlyList<IStyle> Inputs, YGAlign AlignContent) : YogaStyle(Inputs)
    {
        /// <summary>
        /// Tells whether <paramref name="align"/> is an align-content value. <c>Auto</c> and <c>Baseline</c> are rejected,
        /// since lines have no baseline to align and nothing to inherit from.
        /// </summary>
        internal static bool IsSupported(YGAlign align) =>
            align is not (YGAlign.Auto or YGAlign.Baseline);

        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(AlignContent, nameof(AlignContent), IsSupported);
            node->SetAlignContent(AlignContent);
        }
    }
}
