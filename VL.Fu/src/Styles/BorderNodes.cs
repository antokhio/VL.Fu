using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Shared base of the border setters, which all write <see cref="BorderStyle"/> or <see cref="BorderLRTBStyle"/>.
    /// The border only takes layout space; nothing is drawn by it. Negative values are ignored.
    /// </summary>
    [ProcessNode]
    public abstract class BorderStyleNode : EdgesStyleNode
    {
        protected BorderStyleNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        protected sealed override IStyle BuildEdgeStyle(IReadOnlyList<IStyle> inputs, YGEdge edge, float value) =>
            new BorderStyle(inputs, edge, value);

        protected sealed override IStyle BuildEdgesStyle(IReadOnlyList<IStyle> inputs, float left, float right, float top, float bottom) =>
            new BorderLRTBStyle(inputs, left, right, top, bottom);
    }

    /// <summary>
    /// Sets the border width of one Yoga edge in points, all of them by default. Start and End follow the layout direction.
    /// The edge overrides earlier borders on the edges it covers.
    /// Without a value the border is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetBorder")]
    public sealed class SetBorderNode : BorderStyleNode
    {
        public SetBorderNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetBorder(Optional<float> border = default, YGEdge edge = YGEdge.All) =>
            SetEdgeCore(ValidateEdge(border, "Border"), edge);
    }

    /// <summary>
    /// Sets the border width of all four edges in points, overriding earlier border setters.
    /// Without a value the border is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetBorder (Uniform)")]
    public sealed class SetBorderUniformNode : BorderStyleNode
    {
        public SetBorderUniformNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetBorderUniform(Optional<float> border = default) =>
            SetEdgeCore(ValidateEdge(border, "Border"), YGEdge.All);
    }

    /// <summary>
    /// Sets the border width of the left and right edges in points, leaving top and bottom to earlier setters.
    /// Without a value the border is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetBorder (Horizontal)")]
    public sealed class SetBorderHorizontalNode : BorderStyleNode
    {
        public SetBorderHorizontalNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetBorderHorizontal(Optional<float> horizontal = default) =>
            SetEdgeCore(ValidateEdge(horizontal, "Horizontal"), YGEdge.Horizontal);
    }

    /// <summary>
    /// Sets the border width of the top and bottom edges in points, leaving left and right to earlier setters.
    /// Without a value the border is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetBorder (Vertical)")]
    public sealed class SetBorderVerticalNode : BorderStyleNode
    {
        public SetBorderVerticalNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetBorderVertical(Optional<float> vertical = default) =>
            SetEdgeCore(ValidateEdge(vertical, "Vertical"), YGEdge.Vertical);
    }

    /// <summary>
    /// Sets the border width of each edge in points: left, right, top and bottom.
    /// An edge without a value is not set here, so an earlier setter or the default of 0 holds for it.
    /// </summary>
    [ProcessNode(Name = "SetBorder (LRTB)")]
    public sealed class SetBorderLRTBNode : BorderStyleNode
    {
        public SetBorderLRTBNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetBorderLRTB(
            Optional<float> left = default,
            Optional<float> right = default,
            Optional<float> top = default,
            Optional<float> bottom = default
        ) =>
            SetEdgesCore(
                ValidateEdge(left, "Left"),
                ValidateEdge(right, "Right"),
                ValidateEdge(top, "Top"),
                ValidateEdge(bottom, "Bottom")
            );
    }
}
