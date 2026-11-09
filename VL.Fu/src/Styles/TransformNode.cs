using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets the transformation of the node. Without a value it is not set here, so an earlier setter or identity holds.
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
}
