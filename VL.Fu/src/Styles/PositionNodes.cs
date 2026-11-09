using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets how the position offsets place the node: relative to its place in the flow, or absolute inside its parent.
    /// Without a value the type is not set here, so an earlier setter or the default, relative, holds.
    /// </summary>
    [ProcessNode(Name = "SetPositionType")]
    public sealed class SetPositionTypeNode : StyleNode
    {
        private Optional<YGPositionType> _positionType;

        public SetPositionTypeNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetPositionType(Optional<YGPositionType> positionType = default)
        {
            if (SetValue(ref _positionType, ValidateEnum(positionType, "PositionType")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _positionType.HasValue ? new PositionTypeStyle(inputs, _positionType.Value) : null;
    }

    /// <summary>
    /// Sets the horizontal position offset in points, from the left.
    /// Without a value the offset is not set here, so an earlier setter or the default, none, holds.
    /// </summary>
    [ProcessNode(Name = "SetPositionX")]
    public sealed class SetPositionXNode : StyleNode
    {
        private Optional<float> _x;

        public SetPositionXNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetPositionX(Optional<float> x = default)
        {
            if (SetValue(ref _x, ValidateFinite(x, "PositionX")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _x.HasValue ? new PositionStyle(inputs, _x.Value, float.NaN) : null;
    }

    /// <summary>
    /// Sets the vertical position offset in points, from the top.
    /// Without a value the offset is not set here, so an earlier setter or the default, none, holds.
    /// </summary>
    [ProcessNode(Name = "SetPositionY")]
    public sealed class SetPositionYNode : StyleNode
    {
        private Optional<float> _y;

        public SetPositionYNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetPositionY(Optional<float> y = default)
        {
            if (SetValue(ref _y, ValidateFinite(y, "PositionY")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _y.HasValue ? new PositionStyle(inputs, float.NaN, _y.Value) : null;
    }

    /// <summary>
    /// Sets both position offsets in points, X from the left and Y from the top.
    /// Without a value the offsets are not set here, so earlier setters or the defaults hold.
    /// </summary>
    [ProcessNode(Name = "SetPosition")]
    public sealed class SetPositionNode : StyleNode
    {
        private Optional<Vector2> _position;

        public SetPositionNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetPosition(Optional<Vector2> position = default)
        {
            if (SetValue(ref _position, ValidateFinite(position, "Position")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _position.HasValue ? new PositionStyle(inputs, _position.Value.X, _position.Value.Y) : null;
    }
}
