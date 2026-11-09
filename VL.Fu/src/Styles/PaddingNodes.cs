using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Shared base of the padding setters, which all write <see cref="PaddingStyle"/> or <see cref="PaddingLRTBStyle"/>.
    /// Negative values are ignored.
    /// </summary>
    [ProcessNode]
    public abstract class PaddingStyleNode : EdgesStyleNode
    {
        protected PaddingStyleNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        protected sealed override IStyle BuildEdgeStyle(IReadOnlyList<IStyle> inputs, YGEdge edge, float value) =>
            new PaddingStyle(inputs, edge, value);

        protected sealed override IStyle BuildEdgesStyle(IReadOnlyList<IStyle> inputs, float left, float right, float top, float bottom) =>
            new PaddingLRTBStyle(inputs, left, right, top, bottom);
    }

    /// <summary>
    /// Sets the padding of one Yoga edge in points, all of them by default. Start and End follow the layout direction.
    /// The edge overrides earlier padding on the edges it covers.
    /// Without a value the padding is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetPadding")]
    public sealed class SetPaddingNode : PaddingStyleNode
    {
        public SetPaddingNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetPadding(Optional<float> padding = default, YGEdge edge = YGEdge.All) =>
            SetEdgeCore(ValidateEdge(padding, "Padding"), edge);
    }

    /// <summary>
    /// Sets the padding of all four edges in points, overriding earlier padding setters.
    /// Without a value the padding is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetPadding (Uniform)")]
    public sealed class SetPaddingUniformNode : PaddingStyleNode
    {
        public SetPaddingUniformNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetPaddingUniform(Optional<float> padding = default) =>
            SetEdgeCore(ValidateEdge(padding, "Padding"), YGEdge.All);
    }

    /// <summary>
    /// Sets the padding of the left and right edges in points, leaving top and bottom to earlier setters.
    /// Without a value the padding is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetPadding (Horizontal)")]
    public sealed class SetPaddingHorizontalNode : PaddingStyleNode
    {
        public SetPaddingHorizontalNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetPaddingHorizontal(Optional<float> horizontal = default) =>
            SetEdgeCore(ValidateEdge(horizontal, "Horizontal"), YGEdge.Horizontal);
    }

    /// <summary>
    /// Sets the padding of the top and bottom edges in points, leaving left and right to earlier setters.
    /// Without a value the padding is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetPadding (Vertical)")]
    public sealed class SetPaddingVerticalNode : PaddingStyleNode
    {
        public SetPaddingVerticalNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetPaddingVertical(Optional<float> vertical = default) =>
            SetEdgeCore(ValidateEdge(vertical, "Vertical"), YGEdge.Vertical);
    }

    /// <summary>
    /// Sets the padding of each edge in points: left, right, top and bottom.
    /// An edge without a value is not set here, so an earlier setter or the default of 0 holds for it.
    /// </summary>
    [ProcessNode(Name = "SetPadding (LRTB)")]
    public sealed class SetPaddingLRTBNode : PaddingStyleNode
    {
        public SetPaddingLRTBNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetPaddingLRTB(
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
