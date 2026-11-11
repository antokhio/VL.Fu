using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Recognizes mouse input that Windows made up from a touch (v1 TP), so a tap doesn't act twice.
    /// </summary>
    /// <remarks>
    /// The root reports a touch that reaches the tree as processed, which stops the promotion at its source.
    /// This is the backstop for hosts that promote anyway: mouse input is ignored while a touch is down
    /// and for <see cref="QuietMs"/> after the last one lifted. A device loss forgets the touches,
    /// so a lost Up can't block the mouse.
    /// </remarks>
    internal sealed class TouchPromotionGuard
    {
        /// <summary>
        /// How long mouse input is ignored after the last touch lifted, in milliseconds.
        /// </summary>
        public const long QuietMs = 100;

        private readonly HashSet<int> _down = new();
        private long? _lastUpMs;

        /// <summary>
        /// Whether <paramref name="notification"/> is mouse input that arrives during or right after a touch.
        /// A lost mouse is never suppressed.
        /// </summary>
        public bool Suppresses(INotification notification, long nowMs) =>
            notification is MouseNotification and not MouseLostNotification
            && (_down.Count > 0 || (_lastUpMs is { } up && nowMs - up < QuietMs));

        /// <summary>
        /// Follows touches going down and up, and forgets them on a device loss.
        /// </summary>
        public void Track(INotification notification, long nowMs)
        {
            switch (notification)
            {
                case TouchNotification { Kind: TouchNotificationKind.TouchDown } touch:
                    _down.Add(touch.Id);
                    break;
                case TouchNotification { Kind: TouchNotificationKind.TouchUp } touch:
                    _down.Remove(touch.Id);
                    _lastUpMs = nowMs;
                    break;
                case MouseLostNotification or LostFocusNotification:
                    _down.Clear();
                    break;
            }
        }
    }
}
