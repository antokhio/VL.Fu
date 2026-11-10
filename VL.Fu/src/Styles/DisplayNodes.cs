using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets whether the node takes part in the layout. None hides the node and its subtree; they are neither laid out nor rendered.
    /// Without a value the display is not set here, so an earlier setter or the default, flex, holds.
    /// </summary>
    [ProcessNode(Name = "SetDisplay")]
    public sealed class SetDisplayNode : StyleNode
    {
        private Optional<YGDisplay> _display;

        public SetDisplayNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetDisplay(Optional<YGDisplay> display = default)
        {
            if (SetValue(ref _display, ValidateEnum(display, "Display")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _display.HasValue ? new DisplayStyle(inputs, _display.Value) : null;
    }

    /// <summary>
    /// Sets how the node treats children that don't fit: Hidden and Scroll clip them to the node's padding box,
    /// and Scroll also lets them grow past the node along its main axis in the layout. The node's own layer is not clipped.
    /// Without a value the overflow is not set here, so an earlier setter or the default, visible, holds.
    /// </summary>
    [ProcessNode(Name = "SetOverflow")]
    public sealed class SetOverflowNode : StyleNode
    {
        private Optional<YGOverflow> _overflow;

        public SetOverflowNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetOverflow(Optional<YGOverflow> overflow = default)
        {
            if (SetValue(ref _overflow, ValidateEnum(overflow, "Overflow")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _overflow.HasValue ? new OverflowStyle(inputs, _overflow.Value) : null;
    }

    /// <summary>
    /// Sets the layout direction of the node and the subtree that inherits it: left to right or right to left.
    /// Without a value the direction is not set here, so an earlier setter or the default, inherit, holds.
    /// </summary>
    [ProcessNode(Name = "SetDirection")]
    public sealed class SetDirectionNode : StyleNode
    {
        private Optional<YGDirection> _direction;

        public SetDirectionNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetDirection(Optional<YGDirection> direction = default)
        {
            if (SetValue(ref _direction, ValidateEnum(direction, "Direction")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _direction.HasValue ? new DirectionStyle(inputs, _direction.Value) : null;
    }

    /// <summary>
    /// Sets whether width and height include padding and border (border box) or measure the content only (content box).
    /// Without a value the box sizing is not set here, so an earlier setter or the default, border box, holds.
    /// </summary>
    [ProcessNode(Name = "SetBoxSizing")]
    public sealed class SetBoxSizingNode : StyleNode
    {
        private Optional<YGBoxSizing> _boxSizing;

        public SetBoxSizingNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetBoxSizing(Optional<YGBoxSizing> boxSizing = default)
        {
            if (SetValue(ref _boxSizing, ValidateEnum(boxSizing, "BoxSizing")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _boxSizing.HasValue ? new BoxSizingStyle(inputs, _boxSizing.Value) : null;
    }
}
