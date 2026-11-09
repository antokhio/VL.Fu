using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets the max width in points: Yoga never lays the node out wider, unless a min width asks for more.
    /// Without a value the max width is not set here, so an earlier setter or no maximum holds.
    /// </summary>
    [ProcessNode(Name = "SetMaxWidth")]
    public sealed class SetMaxWidthNode : StyleNode
    {
        private Optional<float> _maxWidth;

        public SetMaxWidthNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetMaxWidth(Optional<float> maxWidth = default)
        {
            if (SetValue(ref _maxWidth, ValidateFinite(maxWidth, "MaxWidth")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _maxWidth.HasValue ? new MaxWidthStyle(inputs, YGValue.Point(_maxWidth.Value)) : null;
    }

    /// <summary>
    /// Sets the max width relative to the parent's width, 1 being the whole parent.
    /// Without a value the max width is not set here, so an earlier setter or no maximum holds.
    /// </summary>
    [ProcessNode(Name = "SetMaxWidth (Ratio)")]
    public sealed class SetMaxWidthRatioNode : StyleNode
    {
        private Optional<float> _ratio;

        public SetMaxWidthRatioNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetMaxWidthRatio(Optional<float> ratio = default)
        {
            if (SetValue(ref _ratio, ValidateFinite(ratio, "Ratio")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _ratio.HasValue ? new MaxWidthStyle(inputs, YogaValues.Ratio(_ratio.Value)) : null;
    }

    /// <summary>
    /// Sets the max height in points: Yoga never lays the node out taller, unless a min height asks for more.
    /// Without a value the max height is not set here, so an earlier setter or no maximum holds.
    /// </summary>
    [ProcessNode(Name = "SetMaxHeight")]
    public sealed class SetMaxHeightNode : StyleNode
    {
        private Optional<float> _maxHeight;

        public SetMaxHeightNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetMaxHeight(Optional<float> maxHeight = default)
        {
            if (SetValue(ref _maxHeight, ValidateFinite(maxHeight, "MaxHeight")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _maxHeight.HasValue ? new MaxHeightStyle(inputs, YGValue.Point(_maxHeight.Value)) : null;
    }

    /// <summary>
    /// Sets the max height relative to the parent's height, 1 being the whole parent.
    /// Without a value the max height is not set here, so an earlier setter or no maximum holds.
    /// </summary>
    [ProcessNode(Name = "SetMaxHeight (Ratio)")]
    public sealed class SetMaxHeightRatioNode : StyleNode
    {
        private Optional<float> _ratio;

        public SetMaxHeightRatioNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetMaxHeightRatio(Optional<float> ratio = default)
        {
            if (SetValue(ref _ratio, ValidateFinite(ratio, "Ratio")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _ratio.HasValue ? new MaxHeightStyle(inputs, YogaValues.Ratio(_ratio.Value)) : null;
    }
}
