using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets the min width in points: Yoga never lays the node out narrower.
    /// Without a value the min width is not set here, so an earlier setter or no minimum holds.
    /// </summary>
    [ProcessNode(Name = "SetMinWidth")]
    public sealed class SetMinWidthNode : StyleNode
    {
        private Optional<float> _minWidth;

        public SetMinWidthNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetMinWidth(Optional<float> minWidth = default)
        {
            if (SetValue(ref _minWidth, ValidateFinite(minWidth, "MinWidth")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _minWidth.HasValue ? new MinWidthStyle(inputs, YGValue.Point(_minWidth.Value)) : null;
    }

    /// <summary>
    /// Sets the min width relative to the parent's width, 1 being the whole parent.
    /// Without a value the min width is not set here, so an earlier setter or no minimum holds.
    /// </summary>
    [ProcessNode(Name = "SetMinWidth (Ratio)")]
    public sealed class SetMinWidthRatioNode : StyleNode
    {
        private Optional<float> _ratio;

        public SetMinWidthRatioNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetMinWidthRatio(Optional<float> ratio = default)
        {
            if (SetValue(ref _ratio, ValidateFinite(ratio, "Ratio")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _ratio.HasValue ? new MinWidthStyle(inputs, YogaValues.Ratio(_ratio.Value)) : null;
    }

    /// <summary>
    /// Sets the min height in points: Yoga never lays the node out shorter.
    /// Without a value the min height is not set here, so an earlier setter or no minimum holds.
    /// </summary>
    [ProcessNode(Name = "SetMinHeight")]
    public sealed class SetMinHeightNode : StyleNode
    {
        private Optional<float> _minHeight;

        public SetMinHeightNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetMinHeight(Optional<float> minHeight = default)
        {
            if (SetValue(ref _minHeight, ValidateFinite(minHeight, "MinHeight")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _minHeight.HasValue ? new MinHeightStyle(inputs, YGValue.Point(_minHeight.Value)) : null;
    }

    /// <summary>
    /// Sets the min height relative to the parent's height, 1 being the whole parent.
    /// Without a value the min height is not set here, so an earlier setter or no minimum holds.
    /// </summary>
    [ProcessNode(Name = "SetMinHeight (Ratio)")]
    public sealed class SetMinHeightRatioNode : StyleNode
    {
        private Optional<float> _ratio;

        public SetMinHeightRatioNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetMinHeightRatio(Optional<float> ratio = default)
        {
            if (SetValue(ref _ratio, ValidateFinite(ratio, "Ratio")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _ratio.HasValue ? new MinHeightStyle(inputs, YogaValues.Ratio(_ratio.Value)) : null;
    }
}
