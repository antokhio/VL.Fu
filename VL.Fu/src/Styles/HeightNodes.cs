using Fu.Core.Styles;
using Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets the height in points. Without a value the height is not set here, so an earlier setter or the default holds.
    /// </summary>
    [ProcessNode(Name = "SetHeight")]
    public sealed class SetHeightNode : StyleNode
    {
        private Optional<float> _height;

        public SetHeightNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetHeight(Optional<float> height = default)
        {
            if (SetValue(ref _height, ValidateFinite(height, "Height")))
                RebuildStyle();
        }

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) =>
            _height.HasValue ? new HeightStyle(inputs, YGValue.Point(_height.Value)) : null;
    }

    /// <summary>
    /// Sets the height relative to the parent's height, 1 being the whole parent.
    /// Without a value the height is not set here, so an earlier setter or the default holds.
    /// </summary>
    /// <remarks>
    /// Yoga resolves it only when the parent has a definite height; otherwise the node sizes as if it had no height set.
    /// </remarks>
    [ProcessNode(Name = "SetHeight (Ratio)")]
    public sealed class SetHeightRatioNode : StyleNode
    {
        private Optional<float> _ratio;

        public SetHeightRatioNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetHeightRatio(Optional<float> ratio = default)
        {
            if (SetValue(ref _ratio, ValidateFinite(ratio, "Ratio")))
                RebuildStyle();
        }

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) =>
            _ratio.HasValue ? new HeightStyle(inputs, YogaValues.Ratio(_ratio.Value)) : null;
    }

    /// <summary>
    /// Lets Yoga size the height from the content and the flex settings, overriding earlier height setters.
    /// </summary>
    [ProcessNode(Name = "SetHeight (Auto)")]
    public sealed class SetHeightAutoNode : StyleNode
    {
        public SetHeightAutoNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) =>
            new HeightStyle(inputs, YGValue.Auto);
    }
}