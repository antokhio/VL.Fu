using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets the transformation of the node, which rotates and scales it around its center.
    /// Without a value it is not set here, so an earlier setter or identity holds.
    /// </summary>
    [ProcessNode(Name = "SetTransform")]
    public sealed class SetTransformNode : StyleNode
    {
        private Optional<Matrix> _transform;

        public SetTransformNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetTransform(Optional<Matrix> transform = default)
        {
            if (SetValue(ref _transform, ValidateFinite(transform, "Transform")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _transform.HasValue ? new TransformStyle(inputs, _transform.Value) : null;
    }

    /// <summary>
    /// Sets the point the node's transformation rotates and scales around, relative to its box:
    /// (0, 0) is the center, (-1, -1) the top-left and (1, 1) the bottom-right corner.
    /// Without a value it is not set here, so an earlier setter or the default, the center, holds.
    /// </summary>
    /// <remarks>
    /// Values beyond -1 and 1 put the origin outside the box.
    /// </remarks>
    [ProcessNode(Name = "SetTransformOrigin")]
    public sealed class SetTransformOriginNode : StyleNode
    {
        private Optional<Vector2> _origin;

        public SetTransformOriginNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetTransformOrigin(Optional<Vector2> origin = default)
        {
            if (SetValue(ref _origin, ValidateFinite(origin, "Origin")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _origin.HasValue ? new TransformOriginStyle(inputs, _origin.Value) : null;
    }
}
