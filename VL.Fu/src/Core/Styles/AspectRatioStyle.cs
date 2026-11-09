using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga aspect ratio, width divided by height: when only one axis is known, Yoga derives the other from it.
    /// Without it both axes are sized on their own.
    /// </summary>
    /// <remarks>
    /// The ratio must be a finite number above 0; Yoga would treat 0 or infinity as unset, and a negative ratio has no meaning.
    /// </remarks>
    public sealed record AspectRatioStyle(IReadOnlyList<IStyle> Inputs, float AspectRatio) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            if (!(AspectRatio > 0 && float.IsFinite(AspectRatio)))
                throw new ArgumentOutOfRangeException(nameof(AspectRatio), AspectRatio, "Aspect ratio must be finite and above 0.");

            node->SetAspectRatio(AspectRatio);
        }
    }
}
