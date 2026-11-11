using SkiaSharp;
using Stride.Core.Mathematics;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// What <see cref="NotificationDispatcher"/> needs from the root it belongs to:
    /// the hit test, and the start of a delivery along the running pass's route.
    /// </summary>
    internal interface INotificationRoot : IInteractive
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
