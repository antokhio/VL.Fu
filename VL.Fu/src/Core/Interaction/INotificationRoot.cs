using SkiaSharp;
using Stride.Core.Mathematics;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// What the dispatcher needs from every node of a Fu tree, which the public <see cref="IInteractive"/> doesn't show.
    /// </summary>
    internal interface IInteractionNode : IInteractive
    {
        /// <summary>
        /// Whether the hit test considers this node at all: laid out, displayed and not <see cref="PointerEvents.None"/>.
        /// Its ancestors aren't looked at.
        /// </summary>
        bool IsHitTestable { get; }
    }

    /// <summary>
    /// What <see cref="NotificationDispatcher"/> needs from the root it belongs to:
    /// the hit test, and the start of a delivery along the running pass's route.
    /// </summary>
    internal interface INotificationRoot : IInteractionNode
    {
        /// <summary>
        /// Delivers the pass's notification along its route, starting at this root, and returns
        /// what the delivery class makes of the answers: true when input was processed, otherwise false.
        /// </summary>
        bool Forward(InteractionPass pass);

        /// <inheritdoc cref="Interactive{TSelf}.TryBuildHitPath"/>
        bool TryBuildHitPath(Vector2 position, SKMatrix parentToWindow, List<IInteractive> path);
    }
}
