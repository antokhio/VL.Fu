using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Fu.Core.Extensions;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// The sender of a notification a Fu node hands to its layer and behaviors. It maps window pixels into the node's space,
    /// so <see cref="INotificationWithSpacePositions.PositionInWorldSpace"/> is in node space, as below any VL transform layer.
    /// </summary>
    /// <remarks>
    /// One is created per node per notification. It describes spaces only: which node is running comes from the delivery,
    /// since a native transform layer inside the node's layer replaces the sender for everything below it.
    /// </remarks>
    public sealed class FuNotificationSender : IWorldSpace2d, IProjectionSpace
    {
        private readonly IInteractive _node;
        private readonly SKMatrix? _windowToNode;
        private readonly SKMatrix? _windowToParent;
        private readonly float _scaling;
        private readonly INotification _original;

        /// <param name="node">The node that forwards the notification.</param>
        /// <param name="nodeToWindow">Maps the node's space into window pixels.</param>
        /// <param name="parentToWindow">Maps the parent's space, or the caller's for a root, into window pixels.</param>
        /// <param name="scaling">The caller's DIP scaling.</param>
        /// <param name="original">The notification the root received, whose projection positions are passed on.</param>
        internal FuNotificationSender(
            IInteractive node,
            SKMatrix nodeToWindow,
            SKMatrix parentToWindow,
            float scaling,
            INotification original
        )
        {
            _node = node;
            _windowToNode = nodeToWindow.TryInvert(out var windowToNode) ? windowToNode : null;
            _windowToParent = parentToWindow.TryInvert(out var windowToParent) ? windowToParent : null;
            _scaling = scaling;
            _original = original;
        }

        /// <summary>
        /// The node that forwarded the notification.
        /// </summary>
        public IInteractive Node => _node;

        /// <summary>
        /// Maps window pixels into the node's own space, with (0, 0) at its top-left corner.
        /// No value while the node is scaled to zero, since then no pixel maps into it.
        /// </summary>
        public Optional<Matrix> WindowToNode => ToOptional(_windowToNode);

        /// <summary>
        /// Maps window pixels into the space of the node's parent, or of its caller for a root.
        /// No value while that space is scaled to zero.
        /// </summary>
        public Optional<Matrix> WindowToParent => ToOptional(_windowToParent);

        /// <summary>
        /// The DIP scaling of the renderer the notification came from.
        /// </summary>
        public float Scaling => _scaling;

        internal SKMatrix? WindowToNodeMatrix => _windowToNode;

        internal SKMatrix? WindowToParentMatrix => _windowToParent;

        /// <summary>
        /// The position of <paramref name="notification"/> in the node's own space. NaN while the node is scaled to zero.
        /// </summary>
        public Vector2 MapFromPixels(INotificationWithPosition notification)
        {
            if (_windowToNode is not { } windowToNode)
                return new Vector2(float.NaN, float.NaN);

            var p = windowToNode.MapPoint(notification.Position.X, notification.Position.Y);
            return new Vector2(p.X, p.Y);
        }

        /// <summary>
        /// The projection positions of the notification the root received: a node's transformation doesn't change them.
        /// </summary>
        public void MapFromPixels(
            INotificationWithPosition notification,
            out Vector2 inNormalizedProjection,
            out Vector2 inProjection
        )
        {
            if (_original is INotificationWithSpacePositions positions)
            {
                inNormalizedProjection = positions.PositionInNormalizedProjectionSpace;
                inProjection = positions.PositionInProjectionSpace;
                return;
            }

            SpaceHelpers.DoMapFromPixels(notification.Position, notification.ClientArea, out inNormalizedProjection, out inProjection);
        }

        private static Optional<Matrix> ToOptional(SKMatrix? matrix) =>
            matrix is { } m ? new Optional<Matrix>(m.ToMatrix()) : default;
    }
}
