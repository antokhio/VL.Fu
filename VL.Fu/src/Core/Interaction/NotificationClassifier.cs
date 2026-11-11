using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Chooses how a notification that reaches a root from outside is delivered, by its type (v2 §3.1).
    /// Only notifications without a pointer meaning are broadcast.
    /// </summary>
    /// <remarks>
    /// Press tracking, the click's Up path and gesture sequences come with the claims (N4); until then every positional
    /// notification takes a fresh hit path. Fu's own notifications are planned by whoever queues them.
    /// </remarks>
    internal static class NotificationClassifier
    {
        public static Delivery Classify(INotification notification) =>
            notification switch
            {
                // Device loss has a position, so it is matched before the positional row.
                MouseLostNotification or LostFocusNotification or KeyboardLostNotification =>
                    new Delivery(DeliveryClass.Control, RouteKind.Broadcast),
                INotificationWithPosition => new Delivery(DeliveryClass.Input, RouteKind.Hit),
                _ => new Delivery(DeliveryClass.Input, RouteKind.Broadcast),
            };
    }
}
