using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Chooses how a notification is delivered by its type (v2 §3.1). Only notifications without a pointer meaning are broadcast.
    /// </summary>
    /// <remarks>
    /// Press tracking, the click's Up path and gesture sequences come with the claims (N4); until then every positional
    /// notification takes a fresh hit path.
    /// </remarks>
    internal static class NotificationClassifier
    {
        public static (DeliveryClass Class, RouteKind Route) Classify(INotification notification) =>
            notification switch
            {
                // Device loss has a position, so it is matched before the positional row.
                MouseLostNotification or LostFocusNotification or KeyboardLostNotification =>
                    (DeliveryClass.Control, RouteKind.Broadcast),
                INotificationWithPosition => (DeliveryClass.Input, RouteKind.Hit),
                _ => (DeliveryClass.Input, RouteKind.Broadcast),
            };
    }
}
