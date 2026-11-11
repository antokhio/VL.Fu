using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Which pointer a notification belongs to, taken from its data, which <see cref="INotification.WithSender"/> copies unchanged.
    /// </summary>
    internal static class PointerIds
    {
        /// <summary>
        /// The mouse is one pointer, whatever buttons are pressed (O1).
        /// </summary>
        public const int Mouse = -1;

        /// <summary>
        /// The pointer id of <paramref name="notification"/>: −1 for the mouse, the touch id for a touch,
        /// the sequence id for a gesture. False for notifications without a pointer.
        /// </summary>
        public static bool TryGet(INotification notification, out int pointerId)
        {
            switch (notification)
            {
                case MouseNotification:
                    pointerId = Mouse;
                    return true;
                case TouchNotification touch:
                    pointerId = touch.Id;
                    return true;
                case GestureNotification gesture:
                    pointerId = gesture.SequenceId;
                    return true;
                default:
                    pointerId = default;
                    return false;
            }
        }
    }
}
