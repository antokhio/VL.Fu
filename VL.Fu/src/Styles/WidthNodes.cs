using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets the width in points. Without a value the width is not set here, so an earlier setter or the default holds.
    /// </summary>
    [ProcessNode(Name = "SetWidth")]
    public sealed class SetWidthNode : StyleNode
    {
        private Optional<float> _width;

        public SetWidthNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetWidth(Optional<float> width = default)
        {
            if (SetValue(ref _width, ValidateFinite(width, "Width")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _width.HasValue ? new WidthStyle(inputs, YGValue.Point(_width.Value)) : null;
    }

    /// <summary>
    /// Sets the width in percent of the parent's width.
    /// Without a value the width is not set here, so an earlier setter or the default holds.
    /// </summary>
    [Obsolete("Use SetWidth (Ratio), where 1 is the whole parent.")]
    [ProcessNode(Name = "SetWidth (Percent)")]
    public sealed class SetWidthPercentNode : StyleNode
    {
        private Optional<float> _percent;

        public SetWidthPercentNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetWidthPercent(Optional<float> percent = default)
        {
            if (SetValue(ref _percent, ValidateFinite(percent, "Percent")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _percent.HasValue ? new WidthStyle(inputs, YGValue.Percent(_percent.Value)) : null;
    }

    /// <summary>
    /// Sets the width relative to the parent's width, 1 being the whole parent.
    /// Without a value the width is not set here, so an earlier setter or the default holds.
    /// </summary>
    [ProcessNode(Name = "SetWidth (Ratio)")]
    public sealed class SetWidthRatioNode : StyleNode
    {
        private Optional<float> _ratio;

        public SetWidthRatioNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetWidthRatio(Optional<float> ratio = default)
        {
            if (SetValue(ref _ratio, ValidateFinite(ratio, "Ratio")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _ratio.HasValue ? new WidthStyle(inputs, YogaValues.Ratio(_ratio.Value)) : null;
    }

    /// <summary>
    /// Lets Yoga size the width from the content and the flex settings, overriding earlier width setters.
    /// </summary>
    [ProcessNode(Name = "SetWidth (Auto)")]
    public sealed class SetWidthAutoNode : StyleNode
    {
        public SetWidthAutoNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            new WidthStyle(inputs, YGValue.Auto);
    }
}
