using Fu.Core.Rendering;
using Fu.Core.Styling;
using Stride.Core.Mathematics;

namespace Fu.Core.Styles
{
    /// <summary>
    /// Sets the transformation of the node, moving, rotating or scaling it around its top-left corner
    /// without affecting the layout.
    /// </summary>
    /// <remarks>
    /// The pass commits only when the styled matrix changed, so a directly set transformation is kept until then,
    /// and removing the style commits identity. Mixing both on one node is defined but not intended.
    /// </remarks>
    public sealed record TransformStyle(IReadOnlyList<Style> Inputs, Matrix Transform) : Style<Renderable>(Inputs)
    {
        public override void Build(Renderable node) => node.StageTransformation(Transform);
    }
}
