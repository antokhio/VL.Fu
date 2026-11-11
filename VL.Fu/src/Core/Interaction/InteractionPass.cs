using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// What a running delivery pass knows: the notification, how and where it is delivered, and which node is acting.
    /// Calls made from anywhere inside a node, even below a native transform layer in its <c>Layer</c>, find it through
    /// <see cref="Current"/> (v2 §4.2).
    /// </summary>
    /// <remarks>
    /// Only <see cref="NotificationDispatcher"/> begins and ends passes, in try/finally. Passes nest: a Fu root inside another
    /// root's layer runs its own, and a query runs inside the pass that asked it. Each restores the one before when it ends.
    /// Passes are pooled per dispatcher, so every field is reset when a pass ends.
    /// </remarks>
    internal sealed class InteractionPass
    {
        [ThreadStatic]
        private static InteractionPass? t_current;

        private readonly List<IInteractive> _path = new();

        private InteractionPass? _outer;
        private NotificationDispatcher? _dispatcher;
        private INotification? _notification;
        private CallerInfo _caller = CallerInfo.Default;
        private DeliveryClass _class;
        private NotificationRoute _route;
        private bool _isQuery;
        private long _timeMs;
        private IInteractive? _node;

        /// <summary>
        /// The pass running on this thread, or null outside every pass.
        /// </summary>
        public static InteractionPass? Current => t_current;

        public NotificationDispatcher Dispatcher => _dispatcher!;

        public IInteractive Root => _dispatcher!.Root;

        /// <summary>
        /// The notification as the root received it.
        /// </summary>
        public INotification Notification => _notification!;

        /// <summary>
        /// The caller the root received the notification with.
        /// </summary>
        public CallerInfo Caller => _caller;

        public DeliveryClass Class => _class;

        public NotificationRoute Route => _route;

        /// <summary>
        /// Whether this pass answers a query: its receivers may only add to the query's accumulator (v2 §4.3).
        /// </summary>
        public bool IsQuery => _isQuery;

        /// <summary>
        /// Milliseconds on the dispatcher's clock when the pass began; a query has the time of the pass that asked it.
        /// </summary>
        public long TimeMs => _timeMs;

        /// <summary>
        /// The node whose layer or behaviors are running right now, or null while the notification travels between nodes.
        /// </summary>
        public IInteractive? Node => _node;

        /// <summary>
        /// The list the route's path is built into. It is reused by the next pass.
        /// </summary>
        public List<IInteractive> PathBuffer => _path;

        public void Begin(
            NotificationDispatcher dispatcher,
            INotification notification,
            CallerInfo caller,
            DeliveryClass deliveryClass,
            bool isQuery,
            long timeMs
        )
        {
            _dispatcher = dispatcher;
            _notification = notification;
            _caller = caller;
            _class = deliveryClass;
            _isQuery = isQuery;
            _timeMs = timeMs;

            _outer = t_current;
            t_current = this;
        }

        public void SetRoute(NotificationRoute route) => _route = route;

        public void End()
        {
            t_current = _outer;

            _outer = null;
            _dispatcher = null;
            _notification = null;
            _caller = CallerInfo.Default;
            _class = default;
            _route = default;
            _isQuery = false;
            _timeMs = 0;
            _node = null;
            _path.Clear();
        }

        /// <summary>
        /// Makes <paramref name="node"/> the acting node while its layer and behaviors run.
        /// Returns the node that acted before, for <see cref="Leave"/>.
        /// </summary>
        public IInteractive? Enter(IInteractive node)
        {
            var previous = _node;
            _node = node;
            return previous;
        }

        public void Leave(IInteractive? previous) => _node = previous;

        /// <summary>
        /// Whether <paramref name="notification"/> is the one this pass delivers. A native layer inside a node's layer re-wraps it
        /// into a new instance, so it is recognized by type and payload: position, client area and modifier keys.
        /// </summary>
        public bool Carries(INotification notification)
        {
            var own = _notification;
            if (own is null)
                return false;

            if (ReferenceEquals(notification, own))
                return true;

            if (notification.GetType() != own.GetType())
                return false;

            if (notification is NotificationBase a && own is NotificationBase b && a.ModifierKeys != b.ModifierKeys)
                return false;

            return notification is not INotificationWithPosition p
                || own is not INotificationWithPosition q
                || (p.Position == q.Position && p.ClientArea == q.ClientArea);
        }
    }
}
