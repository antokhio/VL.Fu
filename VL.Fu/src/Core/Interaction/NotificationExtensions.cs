using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Lets a behavior or a layer inside a Fu node send notifications into that node's subtree (v2 §4.2, §4.3).
    /// Both act from the notification the node is receiving right now, so the node is found without passing it in.
    /// </summary>
    /// <remarks>
    /// A wrong call logs a warning and does nothing: outside a delivery, with a notification the running delivery
    /// isn't delivering, while a query is being answered, or with nothing to send.
    /// </remarks>
    public static class NotificationExtensions
    {
        /// <summary>
        /// Queues <paramref name="toDispatch"/> for the subtree of the node that is receiving <paramref name="notification"/>,
        /// the node itself included. It is delivered to every displayed node there once the current delivery is done,
        /// with the caller the current one came with.
        /// </summary>
        /// <remarks>
        /// The node receives what it dispatches, so a receiver that should ignore its own notifications compares the sender.
        /// </remarks>
        public static void Dispatch(this INotification notification, INotification toDispatch)
        {
            if (!TryGetActingPass(notification, toDispatch, nameof(Dispatch), out var pass))
                return;

            pass.Dispatcher.Enqueue(
                toDispatch,
                pass.Caller,
                new Delivery(DeliveryClass.FanOut, RouteKind.Subtree, pass.Node)
            );
        }

        /// <summary>
        /// Delivers <paramref name="query"/> to the subtree of the node that is receiving <paramref name="notification"/>,
        /// the node itself included, and returns once every displayed node there has seen it.
        /// </summary>
        /// <remarks>
        /// A query collects answers: its receivers add to the accumulator the query carries and change nothing else.
        /// They can neither dispatch nor query themselves.
        /// </remarks>
        public static void Query(this INotification notification, INotification query)
        {
            if (!TryGetActingPass(notification, query, nameof(Query), out var pass))
                return;

            pass.Dispatcher.Query(pass, query);
        }

        private static bool TryGetActingPass(
            INotification notification,
            INotification? payload,
            string call,
            out InteractionPass pass
        )
        {
            pass = null!;
            var current = InteractionPass.Current;
            if (current is null)
            {
                InteractionLog.Warning(
                    (notification?.Sender as FuNotificationSender)?.Node,
                    $"{call} only works while a Fu node receives a notification. It was called outside of one and does nothing."
                );
                return false;
            }

            var warnOn = current.Node ?? current.Root;
            if (notification is null || current.Node is null || !current.Carries(notification))
            {
                InteractionLog.Warning(
                    warnOn,
                    $"{call} needs the notification the node is receiving right now. It got another one and does nothing."
                );
                return false;
            }

            if (current.IsQuery)
            {
                InteractionLog.Warning(
                    warnOn,
                    $"{call} can't be used while a query is answered, since a query only collects answers. It does nothing."
                );
                return false;
            }

            if (payload is null)
            {
                InteractionLog.Warning(warnOn, $"{call} got no notification to send and does nothing.");
                return false;
            }

            pass = current;
            return true;
        }
    }
}
