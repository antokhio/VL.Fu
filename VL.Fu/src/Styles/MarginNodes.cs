using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Shared base of the margin setters, which all write <see cref="MarginStyle"/> or <see cref="MarginLRTBStyle"/>.
    /// Negative values are allowed and pull the node over its neighbours.
    /// </summary>
    [ProcessNode]
    public abstract class MarginStyleNode : EdgesStyleNode
    {
        protected MarginStyleNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        protected override bool AllowsNegative => true;

        protected sealed override IStyle BuildEdgeStyle(IReadOnlyList<IStyle> inputs, YGEdge edge, float value) =>
            new MarginStyle(inputs, edge, value);

        protected sealed override IStyle BuildEdgesStyle(IReadOnlyList<IStyle> inputs, float left, float right, float top, float bottom) =>
            new MarginLRTBStyle(inputs, left, right, top, bottom);
    }

    /// <summary>
    /// Sets the margin of one Yoga edge in points, all of them by default. Start and End follow the layout direction.
    /// The edge overrides earlier margin on the edges it covers.
    /// Without a value the margin is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetMargin")]
    public sealed class SetMarginNode : MarginStyleNode
    {
        public SetMarginNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetMargin(Optional<float> margin = default, YGEdge edge = YGEdge.All) =>
            SetEdgeCore(ValidateEdge(margin, "Margin"), edge);
    }

    /// <summary>
    /// Sets the margin of all four edges in points, overriding earlier margin setters.
    /// Without a value the margin is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetMargin (Uniform)")]
    public sealed class SetMarginUniformNode : MarginStyleNode
    {
        public SetMarginUniformNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetMarginUniform(Optional<float> margin = default) =>
            SetEdgeCore(ValidateEdge(margin, "Margin"), YGEdge.All);
    }

    /// <summary>
    /// Sets the margin of the left and right edges in points, leaving top and bottom to earlier setters.
    /// Without a value the margin is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetMargin (Horizontal)")]
    public sealed class SetMarginHorizontalNode : MarginStyleNode
    {
        public SetMarginHorizontalNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetMarginHorizontal(Optional<float> horizontal = default) =>
            SetEdgeCore(ValidateEdge(horizontal, "Horizontal"), YGEdge.Horizontal);
    }

    /// <summary>
    /// Sets the margin of the top and bottom edges in points, leaving left and right to earlier setters.
    /// Without a value the margin is not set here, so earlier setters or the default of 0 hold.
    /// </summary>
    [ProcessNode(Name = "SetMargin (Vertical)")]
    public sealed class SetMarginVerticalNode : MarginStyleNode
    {
        public SetMarginVerticalNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetMarginVertical(Optional<float> vertical = default) =>
            SetEdgeCore(ValidateEdge(vertical, "Vertical"), YGEdge.Vertical);
    }

    /// <summary>
    /// Sets the margin of each edge in points: left, right, top and bottom.
    /// An edge without a value is not set here, so an earlier setter or the default of 0 holds for it.
    /// </summary>
    [ProcessNode(Name = "SetMargin (LRTB)")]
    public sealed class SetMarginLRTBNode : MarginStyleNode
    {
        public SetMarginLRTBNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetMarginLRTB(
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
