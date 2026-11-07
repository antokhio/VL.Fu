using Fu.Core.Styling;
using YogaSharp;

namespace Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga gap between children for one gutter. A row or column gap beats the gap for all gutters,
    /// regardless of the order the instructions run in.
    /// </summary>
    public sealed record GapStyle(IReadOnlyList<Style> Inputs, YGGutter Gutter, float Value) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => node->SetGap(Gutter, Value);
    }
}
