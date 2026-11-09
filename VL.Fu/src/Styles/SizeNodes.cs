using Fu.Core.Styles;
using Fu.Core.Styling;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets width and height in points, X being the width and Y the height.
    /// Without a value the size is not set here, so earlier setters or the defaults hold.
    /// </summary>
    [ProcessNode(Name = "SetSize")]
    public sealed class SetSizeNode : StyleNode
    {
        private Optional<Vector2> _size;

        public SetSizeNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetSize(Optional<Vector2> size = default)
        {
            if (SetValue(ref _size, ValidateFinite(size, "Size")))
                RebuildStyle();
        }

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) =>
            _size.HasValue
                ? new SizeStyle(inputs, YGValue.Point(_size.Value.X), YGValue.Point(_size.Value.Y))
                : null;
    }

    /// <summary>
    /// Sets width and height relative to the parent's size, 1 being the whole parent on that axis.
    /// Without a value the size is not set here, so earlier setters or the defaults hold.
    /// </summary>
    [ProcessNode(Name = "SetSize (Ratio)")]
    public sealed class SetSizeRatioNode : StyleNode
    {
        private Optional<Vector2> _ratio;

        public SetSizeRatioNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetSizeRatio(Optional<Vector2> ratio = default)
        {
            if (SetValue(ref _ratio, ValidateFinite(ratio, "Ratio")))
                RebuildStyle();
        }

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) =>
            _ratio.HasValue
                ? new SizeStyle(inputs, YogaValues.Ratio(_ratio.Value.X), YogaValues.Ratio(_ratio.Value.Y))
                : null;
    }

    /// <summary>
    /// Lets Yoga size both axes from the content and the flex settings, overriding earlier size setters.
    /// </summary>
    [ProcessNode(Name = "SetSize (Auto)")]
    public sealed class SetSizeAutoNode : StyleNode
    {
        public SetSizeAutoNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) =>
            new SizeStyle(inputs, YGValue.Auto, YGValue.Auto);
    }
}