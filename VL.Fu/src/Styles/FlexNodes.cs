using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets the main axis the children are laid out along.
    /// Without a value the direction is not set here, so an earlier setter or the default row holds.
    /// </summary>
    [ProcessNode(Name = "SetFlexDirection")]
    public sealed class SetFlexDirectionNode : StyleNode
    {
        private Optional<YGFlexDirection> _flexDirection;

        public SetFlexDirectionNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetFlexDirection(Optional<YGFlexDirection> flexDirection = default)
        {
            if (SetValue(ref _flexDirection, ValidateEnum(flexDirection, "FlexDirection")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _flexDirection.HasValue ? new FlexDirectionStyle(inputs, _flexDirection.Value) : null;
    }

    /// <summary>
    /// Sets whether children that don't fit the main axis move to a new line.
    /// Without a value the wrap is not set here, so an earlier setter or the default, no wrap, holds.
    /// </summary>
    [ProcessNode(Name = "SetFlexWrap")]
    public sealed class SetFlexWrapNode : StyleNode
    {
        private Optional<YGWrap> _flexWrap;

        public SetFlexWrapNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetFlexWrap(Optional<YGWrap> flexWrap = default)
        {
            if (SetValue(ref _flexWrap, ValidateEnum(flexWrap, "FlexWrap")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _flexWrap.HasValue ? new FlexWrapStyle(inputs, _flexWrap.Value) : null;
    }

    /// <summary>
    /// Sets this node's share of the free space along its parent's main axis. A negative value is ignored.
    /// Without a value the grow is not set here, so an earlier setter or the default of 0 holds.
    /// </summary>
    [ProcessNode(Name = "SetFlexGrow")]
    public sealed class SetFlexGrowNode : StyleNode
    {
        private Optional<float> _flexGrow;

        public SetFlexGrowNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetFlexGrow(Optional<float> flexGrow = default)
        {
            if (SetValue(ref _flexGrow, ValidateNonNegative(flexGrow, "FlexGrow")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _flexGrow.HasValue ? new FlexGrowStyle(inputs, _flexGrow.Value) : null;
    }

    /// <summary>
    /// Sets this node's share of the overflow to give up along its parent's main axis. A negative value is ignored.
    /// Without a value the shrink is not set here, so an earlier setter or the default of 1 holds.
    /// </summary>
    [ProcessNode(Name = "SetFlexShrink")]
    public sealed class SetFlexShrinkNode : StyleNode
    {
        private Optional<float> _flexShrink;

        public SetFlexShrinkNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetFlexShrink(Optional<float> flexShrink = default)
        {
            if (SetValue(ref _flexShrink, ValidateNonNegative(flexShrink, "FlexShrink")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _flexShrink.HasValue ? new FlexShrinkStyle(inputs, _flexShrink.Value) : null;
    }

    /// <summary>
    /// Sets the size along the parent's main axis in points, before free space is shared out.
    /// Without a value the basis is not set here, so an earlier setter or the default auto holds.
    /// </summary>
    [ProcessNode(Name = "SetFlexBasis")]
    public sealed class SetFlexBasisNode : StyleNode
    {
        private Optional<float> _flexBasis;

        public SetFlexBasisNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetFlexBasis(Optional<float> flexBasis = default)
        {
            if (SetValue(ref _flexBasis, ValidateFinite(flexBasis, "FlexBasis")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _flexBasis.HasValue ? new FlexBasisStyle(inputs, YGValue.Point(_flexBasis.Value)) : null;
    }

    /// <summary>
    /// Sets the size along the parent's main axis relative to the parent's size on that axis, 1 being the whole parent.
    /// Without a value the basis is not set here, so an earlier setter or the default auto holds.
    /// </summary>
    [ProcessNode(Name = "SetFlexBasis (Ratio)")]
    public sealed class SetFlexBasisRatioNode : StyleNode
    {
        private Optional<float> _ratio;

        public SetFlexBasisRatioNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetFlexBasisRatio(Optional<float> ratio = default)
        {
            if (SetValue(ref _ratio, ValidateFinite(ratio, "Ratio")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _ratio.HasValue ? new FlexBasisStyle(inputs, YogaValues.Ratio(_ratio.Value)) : null;
    }

    /// <summary>
    /// Takes the basis from the node's width or height on the parent's main axis, overriding earlier basis setters.
    /// </summary>
    [ProcessNode(Name = "SetFlexBasis (Auto)")]
    public sealed class SetFlexBasisAutoNode : StyleNode
    {
        public SetFlexBasisAutoNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            new FlexBasisStyle(inputs, YGValue.Auto);
    }
}
