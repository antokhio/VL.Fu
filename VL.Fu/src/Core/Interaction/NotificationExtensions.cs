using VL.Fu.Core.Tree;
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
        /// Queues <paramref name="toDispatch"/> for the subtree of <paramref name="node"/>, which is the node receiving
        /// <paramref name="notification"/> or one of its ancestors. Otherwise it works like <see cref="Dispatch"/>.
        /// </summary>
        /// <remarks>
        /// An item uses it to send into the subtree of the scope it belongs to, such as its selection provider's.
        /// A node that isn't on the way up from the receiving node is reported, and nothing is sent.
        /// </remarks>
        internal static void DispatchInto(this INotification notification, IInteractive node, INotification toDispatch)
        {
            if (!TryGetActingPass(notification, toDispatch, nameof(DispatchInto), out var pass))
                return;

            for (ITraversable? current = pass.Node; current is not null; current = current.Parent)
            {
                if (ReferenceEquals(current, node))
                {
                    pass.Dispatcher.Enqueue(
                        toDispatch,
                        pass.Caller,
                        new Delivery(DeliveryClass.FanOut, RouteKind.Subtree, node)
                    );
                    return;
                }
            }

            InteractionLog.Warning(
                pass.Node,
                $"{nameof(DispatchInto)} can only send into the receiving node or one of its ancestors. It does nothing."
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

        /// <summary>
        /// Asks for the pointer <paramref name="notification"/> belongs to, for the gesture <paramref name="kind"/>, on behalf of
        /// <paramref name="owner"/> (v2 §5). Returns whether the request was taken; whether it is granted is decided after the
        /// current delivery, and the owner learns it from a <see cref="PointerCaptureNotification"/> that grants it.
        /// </summary>
        /// <remarks>
        /// The node receiving the notification is the one that claims: while the claim holds, the pointer's input goes to it alone,
        /// and the others on the pointer's path get a <see cref="PointerCancelNotification"/>. The pointer has to be pressed
        /// on that node, or be pressed by this very notification. The owner, usually the behavior, releases the claim with
        /// <see cref="ReleasePointerCapture"/>; otherwise it ends with the last Up.
        /// </remarks>
        public static bool RequestPointerCapture(this INotification notification, object owner, GestureKind kind) =>
            RequestPointerCapture(notification, owner, kind, Array.Empty<int>());

        /// <summary>
        /// Asks for the pointer <paramref name="notification"/> belongs to together with <paramref name="pointerIds"/>, for the
        /// gesture <paramref name="kind"/>, on behalf of <paramref name="owner"/>, as a pinch claims both its fingers.
        /// Returns whether the request was taken.
        /// </summary>
        /// <remarks>
        /// Every pointer has to be pressed on the node receiving the notification. The notification's own pointer always
        /// belongs to the request, whether <paramref name="pointerIds"/> names it or not.
        /// </remarks>
        public static bool RequestPointerCapture(
            this INotification notification,
            object owner,
            GestureKind kind,
            IReadOnlyList<int> pointerIds
        )
        {
            const string call = nameof(RequestPointerCapture);
            if (!TryGetActingPass(notification, call, out var pass))
                return false;

            var node = pass.Node!;
            if (owner is null)
            {
                InteractionLog.Warning(node, $"{call} needs an owner, usually the behavior asking. It got none and does nothing.");
                return false;
            }

            if (!Enum.IsDefined(kind))
            {
                InteractionLog.Warning(node, $"{call} got the unknown gesture kind {(int)kind} and does nothing.");
                return false;
            }

            if (notification is GestureNotification || !PointerIds.TryGet(notification, out var own))
            {
                InteractionLog.Warning(
                    node,
                    $"{call} needs a mouse or touch notification, since only a pressed pointer can be claimed. It does nothing."
                );
                return false;
            }

            var pointers = new List<int> { own };
            if (pointerIds is not null)
            {
                for (var i = 0; i < pointerIds.Count; i++)
                {
                    if (!pointers.Contains(pointerIds[i]))
                        pointers.Add(pointerIds[i]);
                }
            }

            foreach (var pointerId in pointers)
            {
                if (pass.Dispatcher.Claims.CheckRequest(pass, pointerId, node) is { } problem)
                {
                    InteractionLog.Warning(node, $"{call} can't be granted: {problem}. It does nothing.");
                    return false;
                }
            }

            pass.AddRequest(new ClaimRequest(owner, node, kind, pointers));
            return true;
        }

        /// <summary>
        /// Takes <paramref name="owner"/> out of the claim on the pointer <paramref name="notification"/> belongs to, right away.
        /// When no member is left, the claim ends, and the pointer's input goes back to the path it was pressed on.
        /// </summary>
        /// <remarks>
        /// Nobody is notified, since the owner chose to leave. Releasing a pointer that isn't claimed, or claimed by others, does nothing.
        /// </remarks>
        public static void ReleasePointerCapture(this INotification notification, object owner)
        {
            const string call = nameof(ReleasePointerCapture);
            if (!TryGetActingPass(notification, call, out var pass))
                return;

            if (owner is null || !PointerIds.TryGet(notification, out var pointerId))
            {
                InteractionLog.Warning(
                    pass.Node,
                    $"{call} needs an owner and a notification that belongs to a pointer. It does nothing."
                );
                return;
            }

            pass.Dispatcher.Claims.Release(pointerId, owner);
        }

        private static bool TryGetActingPass(
            INotification notification,
            INotification? payload,
            string call,
            out InteractionPass pass
        )
        {
            if (!TryGetActingPass(notification, call, out pass))
                return false;

            if (payload is null)
            {
                InteractionLog.Warning(pass.Node, $"{call} got no notification to send and does nothing.");
                pass = null!;
                return false;
            }

            return true;
        }

        private static bool TryGetActingPass(INotification notification, string call, out InteractionPass pass)
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

            pass = current;
            return true;
        }
    }
}
