using Fu.Core.Styling;
using YogaSharp;

namespace Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga width. Points, percent and auto write the same slot, so the later instruction wins.
    /// </summary>
    public sealed record WidthStyle(IReadOnlyList<Style> Inputs, YGValue Width) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            switch (Width.Unit)
            {
                case YGUnit.Point:
                    node->SetWidth(Width.Value);
                    break;
                case YGUnit.Percent:
                    node->SetWidthPercent(Width.Value);
                    break;
                case YGUnit.Auto:
                    node->SetWidthAuto();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Width), Width.Unit, "Unsupported width unit.");
            }
        }
    }
}
