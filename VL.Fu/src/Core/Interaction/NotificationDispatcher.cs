using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Runs the notifications of one Fu root as passes, one at a time (v2 §2.2, §4.1). A pass classifies the notification,
    /// builds its route, lets the root forward it, then does the bookkeeping: touch tracking and the hover diff.
    /// Everything a pass produces is queued and runs after it as a pass of its own, so nothing is delivered
    /// in the middle of another delivery. Queries are the one exception (§4.3).
    /// </summary>
    /// <remarks>
    /// Created lazily by the root's first notification. It never calls behaviors itself: the nodes forward,
    /// and behaviors are reached as VL behaviors.
    /// </remarks>
    internal sealed class NotificationDispatcher
    {
        /// <summary>
        /// How many queued passes one notification from outside may cause before the rest is dropped,
        /// as a guard against behaviors that send each other notifications in a loop.
        /// </summary>
        public const int MaxQueuedPasses = 64;

        private readonly INotificationRoot _root;
        private readonly Queue<QueuedNotification> _outbox = new();
        private readonly Stack<InteractionPass> _pool = new();
        private readonly HoverTracker _hover = new();
        private readonly TouchPromotionGuard _touch = new();
        private readonly List<IInteractive> _left = new();
        private readonly List<IInteractive> _entered = new();

        private TimeProvider _clock = TimeProvider.System;
        private long _origin = TimeProvider.System.GetTimestamp();
        private bool _running;

        public NotificationDispatcher(INotificationRoot root)
        {
            _root = root;
        }

        public IInteractive Root => _root;

        /// <summary>
        /// The nodes the mouse or the primary touch is over, from the root down.
        /// </summary>
        public IReadOnlyList<IInteractive> HoverPath => _hover.Path;

        /// <summary>
        /// Milliseconds on the dispatcher's clock since it was created or the clock was set.
        /// </summary>
        public long NowMs => (long)_clock.GetElapsedTime(_origin).TotalMilliseconds;

        /// <summary>
        /// Replaces the clock and starts <see cref="NowMs"/> from zero. For tests.
        /// </summary>
        internal void SetClock(TimeProvider clock)
        {
            _clock = clock;
            _origin = clock.GetTimestamp();
        }

        /// <summary>
        /// Delivers a notification that reached the root from outside, planned by its type.
        /// </summary>
        public bool Run(INotification notification, CallerInfo caller) =>
            Run(notification, caller, NotificationClassifier.Classify(notification));

        /// <summary>
        /// Delivers <paramref name="notification"/> as planned by <paramref name="delivery"/>, then every pass the outbox holds,
        /// first in, first out. Returns whether the notification was processed.
        /// </summary>
        /// <remarks>
        /// Called while a pass is running, as when a behavior notifies the root, it queues the notification and returns false.
        /// If a receiver throws, the pass ends, the outbox is dropped, and the exception goes on to the renderer.
        /// </remarks>
        public bool Run(INotification notification, CallerInfo caller, Delivery delivery)
        {
            if (_running)
            {
                Enqueue(notification, caller, delivery);
                return false;
            }

            _running = true;
            try
            {
                var handled = RunPass(notification, caller, delivery);
                Drain();
                return handled;
            }
            finally
            {
                _running = false;
                _outbox.Clear();
            }
        }

        /// <summary>
        /// Queues <paramref name="notification"/> to run as its own pass after the running one.
        /// </summary>
        public void Enqueue(INotification notification, CallerInfo caller, Delivery delivery) =>
            _outbox.Enqueue(new QueuedNotification(notification, caller, delivery));

        /// <summary>
        /// Delivers <paramref name="query"/> into the subtree of the node acting in <paramref name="outer"/> right away,
        /// as a fan-out inside the running pass, and returns when every node there has seen it.
        /// </summary>
        public void Query(InteractionPass outer, INotification query)
        {
            var pass = Rent();
            try
            {
                pass.Begin(this, query, outer.Caller, DeliveryClass.FanOut, isQuery: true, outer.TimeMs);
                if (TryBuildRoute(pass, new Delivery(DeliveryClass.FanOut, RouteKind.Subtree, outer.Node)))
                    _root.Forward(pass);
            }
            finally
            {
                Return(pass);
            }
        }

        private bool RunPass(INotification notification, CallerInfo caller, Delivery delivery)
        {
            var now = NowMs;
            if (_touch.Suppresses(notification, now))
                return false;

            var pass = Rent();
            try
            {
                pass.Begin(this, notification, caller, delivery.Class, isQuery: false, now);
                var reached = TryBuildRoute(pass, delivery);
                var handled = reached && _root.Forward(pass);

                _touch.Track(notification, now);
                UpdateHover(pass, delivery, reached);

                // A touch that reached the tree counts as processed, so Windows doesn't promote it to mouse input (v1 TP).
                return handled || (reached && notification is TouchNotification);
            }
            finally
            {
                Return(pass);
            }
        }

        private void Drain()
        {
            var passes = 0;
            while (_outbox.Count > 0)
            {
                if (passes == MaxQueuedPasses)
                {
                    InteractionLog.Warning(
                        _root,
                        $"One notification caused more than {MaxQueuedPasses} further notifications, so the rest were dropped. "
                            + "Behaviors that send notifications whenever they receive one can do this to each other."
                    );
                    _outbox.Clear();
                    return;
                }

                var next = _outbox.Dequeue();
                passes++;
                RunPass(next.Notification, next.Caller, next.Delivery);
            }
        }

        /// <summary>
        /// Builds the route of <paramref name="delivery"/> into the pass's path list, as the tree is now (rule 4).
        /// False when it reaches nothing: no node is hit, or the node it names isn't in this root's tree anymore.
        /// </summary>
        private bool TryBuildRoute(InteractionPass pass, Delivery delivery)
        {
            var path = pass.PathBuffer;
            path.Clear();

            switch (delivery.Route)
            {
                case RouteKind.Hit:
                    if (
                        pass.Notification is not INotificationWithPosition positioned
                        || !_root.TryBuildHitPath(positioned.Position, pass.Caller.Transformation, path)
                    )
                        return false;

                    pass.SetRoute(NotificationRoute.Hit(path));
                    return true;

                case RouteKind.Target:
                    if (delivery.Node is null || !NotificationRoute.TryFillPath(_root, delivery.Node, path))
                        return false;

                    pass.SetRoute(NotificationRoute.Target(path));
                    return true;

                case RouteKind.Subtree:
                    if (delivery.Node is null || !NotificationRoute.TryFillPath(_root, delivery.Node, path))
                        return false;

                    pass.SetRoute(NotificationRoute.Subtree(path));
                    return true;

                default:
                    path.Add(_root);
                    pass.SetRoute(NotificationRoute.Broadcast(path));
                    return true;
            }
        }

        /// <summary>
        /// Moves the hover path after a pointer pass and queues the leaves and enters it causes (v2 §3.5).
        /// The mouse hovers its hit path; the primary touch hovers its hit path until it lifts; a lost mouse hovers nothing.
        /// </summary>
        private void UpdateHover(InteractionPass pass, Delivery delivery, bool reached)
        {
            var notification = pass.Notification;
            var hit = reached && delivery.Route == RouteKind.Hit;
            IReadOnlyList<IInteractive> next;
            int pointerId;

            switch (notification)
            {
                case MouseLostNotification:
                    next = Array.Empty<IInteractive>();
                    pointerId = PointerIds.Mouse;
                    break;
                case MouseNotification when delivery.Class == DeliveryClass.Input:
                    next = hit ? pass.Route.Path : Array.Empty<IInteractive>();
                    pointerId = PointerIds.Mouse;
                    break;
                case TouchNotification { Primary: true } touch when delivery.Class == DeliveryClass.Input:
                    next = hit && touch.Kind != TouchNotificationKind.TouchUp ? pass.Route.Path : Array.Empty<IInteractive>();
                    pointerId = touch.Id;
                    break;
                default:
                    return;
            }

            _hover.Update(next, _left, _entered);
            if (_left.Count == 0 && _entered.Count == 0)
                return;

            var modifiers = notification is NotificationBase withKeys ? withKeys.ModifierKeys : Keys.None;
            var sender = notification.Sender;

            foreach (var node in _left)
            {
                Enqueue(
                    new PointerLeaveNotification(pointerId, modifiers, sender),
                    pass.Caller,
                    new Delivery(DeliveryClass.Control, RouteKind.Target, node)
                );
            }

            foreach (var node in _entered)
            {
                Enqueue(
                    new PointerEnterNotification(pointerId, modifiers, sender),
                    pass.Caller,
                    new Delivery(DeliveryClass.Control, RouteKind.Target, node)
                );
            }
        }

        private InteractionPass Rent() => _pool.Count > 0 ? _pool.Pop() : new InteractionPass();

        private void Return(InteractionPass pass)
        {
            pass.End();
            _pool.Push(pass);
        }

        private readonly record struct QueuedNotification(INotification Notification, CallerInfo Caller, Delivery Delivery);
    }
}
