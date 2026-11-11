using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Chooses how a notification that reaches a root from outside is delivered, by its type (v2 §3.1).
    /// Only notifications without a pointer meaning are broadcast.
    /// </summary>
    /// <remarks>
    /// Mouse, touch and gesture input takes a pointer route: the claims choose, per notification, between the claim node,
    /// the path the pointer was pressed on and a fresh hit path. Other positional notifications always take a fresh hit path.
    /// Fu's own notifications are planned by whoever queues them.
    /// </remarks>
    internal static class NotificationClassifier
    {
        public static Delivery Classify(INotification notification) =>
            notification switch
            {
                // Device loss has a position, so it is matched before the pointer rows.
                MouseLostNotification or LostFocusNotification or KeyboardLostNotification =>
                    new Delivery(DeliveryClass.Control, RouteKind.Broadcast),
                MouseNotification or TouchNotification or GestureNotification =>
                    new Delivery(DeliveryClass.Input, RouteKind.Pointer),
                INotificationWithPosition => new Delivery(DeliveryClass.Input, RouteKind.Hit),
                _ => new Delivery(DeliveryClass.Input, RouteKind.Broadcast),
            };
    }
}
