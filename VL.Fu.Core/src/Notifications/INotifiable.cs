using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Notifications
{
    /// <summary>
    /// Defines the contract for a service that can be notified with INotification objects.
    /// This is distinct from Skia's IBehavior interface, as it is designed for a broadcast (publish-subscribe)
    /// pattern within the Fu service hub, not a hierarchical chain of responsibility. It does not return a boolean
    /// or require CallerInfo, as all subscribing services should receive the notification.
    /// </summary>
    public interface INotifiable
    {
        /// <summary>
        /// Pushes a notification to the service for processing.
        /// </summary>
        /// <param name="notification">The notification object.</param>
        void Notify(INotification notification);
    }
}
