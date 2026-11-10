using VL.Fu.Core.Layout;
using VL.Fu.Core.Styling;
using Stride.Core.Mathematics;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the point the node's transformation rotates and scales around, relative to its box:
    /// (0, 0) is the center, (-1, -1) the top-left and (1, 1) the bottom-right corner.
    /// Without it the center holds.
    /// </summary>
    /// <remarks>
    /// Values beyond -1 and 1 put the origin outside the box; both must be finite.
    /// The pass commits only when the styled origin changed, like <see cref="TransformStyle"/>.
    /// </remarks>
    public sealed record TransformOriginStyle(IReadOnlyList<IStyle> Inputs, Vector2 Origin) : Style<IFuNode>(Inputs)
    {
        public override void Build(IFuNode node)
        {
            if (!float.IsFinite(Origin.X) || !float.IsFinite(Origin.Y))
                throw new ArgumentOutOfRangeException(nameof(Origin), Origin, "Transform origin must be finite.");

            // A node only styles itself, so an IFuNode here is a Fu node, which is layoutable.
            ((Layoutable<IFuNode>)node).StageTransformOrigin(Origin);
        }
    }
}
