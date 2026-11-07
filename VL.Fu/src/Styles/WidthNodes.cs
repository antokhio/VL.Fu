using Fu.Core.Styles;
using Fu.Core.Styling;
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

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) =>
            _width.HasValue ? new WidthStyle(inputs, YGValue.Point(_width.Value)) : null;
    }

    /// <summary>
    /// Sets the width in percent of the parent's width.
    /// Without a value the width is not set here, so an earlier setter or the default holds.
    /// </summary>
    [ProcessNode(Name = "SetWidthPercent")]
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

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) =>
            _percent.HasValue ? new WidthStyle(inputs, YGValue.Percent(_percent.Value)) : null;
    }

    /// <summary>
    /// Lets Yoga size the width from the content and the flex settings, overriding earlier width setters.
    /// </summary>
    [ProcessNode(Name = "SetWidthAuto")]
    public sealed class SetWidthAutoNode : StyleNode
    {
        public SetWidthAutoNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) => new WidthStyle(inputs, YGValue.Auto);
    }
}
