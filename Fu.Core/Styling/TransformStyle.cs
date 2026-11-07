using Fu.Core.Rendering;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Model;

namespace Fu.Core.Styling
{
    /// <summary>
    /// Sets the transformation of the node, moving, rotating or scaling it around its top-left corner
    /// without affecting the layout.
    /// </summary>
    /// <remarks>
    /// The pass commits only when the styled matrix changed, so a directly set transformation is kept until then,
    /// and removing the style commits identity. Mixing both on one node is defined but not intended.
    /// </remarks>
    public sealed record TransformStyle(Spread<Style> Inputs, Matrix Transform) : Style<Renderable>(Inputs)
    {
        public override void Build(Renderable node) => node.StageTransformation(Transform);
    }

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
            if (SetValue(ref _transform, transform))
                RebuildStyle();
        }

        protected override Style? BuildStyle(Spread<Style> inputs) =>
            _transform.HasValue ? new TransformStyle(inputs, _transform.Value) : null;
    }
}
