using VL.Fu.Core.Layout;
using VL.Fu.Core.Styling;
using Stride.Core.Mathematics;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the transformation of the node, moving, rotating or scaling it around its center
    /// without affecting the layout.
    /// </summary>
    /// <remarks>
    /// The pass commits only when the styled matrix changed, so a directly set transformation is kept until then,
    /// and removing the style commits identity. Mixing both on one node is defined but not intended.
    /// </remarks>
    public sealed record TransformStyle(IReadOnlyList<IStyle> Inputs, Matrix Transform) : Style<IFuNode>(Inputs)
    {
        // A node only styles itself, so an IFuNode here is a Fu node, which is layoutable.
        public override void Build(IFuNode node) => ((Layoutable<IFuNode>)node).StageTransformation(Transform);
    }
}
