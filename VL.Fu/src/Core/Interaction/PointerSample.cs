using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Lib.IO;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// What a pointer sample reports: a press, a move, a release or a wheel turn.
    /// </summary>
    internal enum PointerSampleKind
    {
        Down,
        Move,
        Up,
        Wheel,
    }

    /// <summary>
    /// One mouse or touch notification as a Fu node received it, flattened by <see cref="PointerInput.TryRead"/>:
    /// which pointer, which button, where in the window, and the spaces of the node that forwarded it.
    /// </summary>
    /// <remarks>
    /// Recognizers read samples and never see a notification. They map <see cref="PositionInWindow"/> through matrices
    /// they froze at press, not through the current ones, so a gesture stays in one space while its node moves.
    /// </remarks>
    internal readonly struct PointerSample
    {
        /// <summary>
        /// A press, a move, a release or a wheel turn.
        /// </summary>
        public PointerSampleKind Kind { get; init; }

        /// <summary>
        /// −1 for the mouse, otherwise the id of the touch.
        /// </summary>
        public int PointerId { get; init; }

        /// <summary>
        /// The button that went down or up; a touch is <see cref="MouseButtons.Left"/>.
        /// <see cref="MouseButtons.None"/> for moves and wheel turns, which don't say which buttons are held.
        /// </summary>
        public MouseButtons Button { get; init; }

        /// <summary>
        /// The modifier keys held when the notification was raised.
        /// </summary>
        public Keys Modifiers { get; init; }

        /// <summary>
        /// The position in window pixels, as the notification carries it.
        /// </summary>
        public Vector2 PositionInWindow { get; init; }

        /// <summary>
        /// Wheel notches turned: X from the horizontal wheel, Y from the vertical one, signed as VL reports them.
        /// Zero for every kind but <see cref="PointerSampleKind.Wheel"/>.
        /// </summary>
        public Vector2 WheelDelta { get; init; }

        /// <summary>
        /// The node that forwarded the notification.
        /// </summary>
        public IInteractive Node { get; init; }

        /// <summary>
        /// Maps window pixels into the node's own space. Null while the node is scaled to zero.
        /// </summary>
        public SKMatrix? WindowToNode { get; init; }

        /// <summary>
        /// Maps window pixels into the space of the node's parent. Null while that space is scaled to zero.
        /// </summary>
        public SKMatrix? WindowToParent { get; init; }

        /// <summary>
        /// Window pixels per DIP.
        /// </summary>
        public float Scaling { get; init; }

        /// <summary>
        /// Milliseconds on the root's clock when the delivery of the notification began.
        /// </summary>
        public long TimeMs { get; init; }

        /// <summary>
        /// The position in the node's own space, through the current matrix.
        /// False while the node is scaled to zero.
        /// </summary>
        public bool TryGetPositionInNode(out Vector2 positionInNode) => TryMap(WindowToNode, out positionInNode);

        /// <summary>
        /// The position in the space of the node's parent, through the current matrix.
        /// False while that space is scaled to zero.
        /// </summary>
        public bool TryGetPositionInParent(out Vector2 positionInParent) => TryMap(WindowToParent, out positionInParent);

        /// <summary>
        /// <paramref name="pixels"/> in DIP. A scaling that isn't a positive number counts as 1.
        /// </summary>
        public float ToDip(float pixels) => pixels / DipScaling(Scaling);

        /// <summary>
        /// <paramref name="scaling"/> when it is a positive number, otherwise 1.
        /// </summary>
        public static float DipScaling(float scaling) => float.IsFinite(scaling) && scaling > 0f ? scaling : 1f;

        private bool TryMap(SKMatrix? matrix, out Vector2 position)
        {
            if (matrix is not { } m)
            {
                position = default;
                return false;
            }

            var p = m.MapPoint(PositionInWindow.X, PositionInWindow.Y);
            position = new Vector2(p.X, p.Y);
            return true;
        }
    }
}
