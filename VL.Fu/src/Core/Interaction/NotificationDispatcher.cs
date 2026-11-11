using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Runs the notifications of one Fu root as passes, one at a time (v2 §2.2, §4.1). A pass classifies the notification,
    /// builds its route, lets the root forward it, then does the bookkeeping: pressed pointers and their claims,
    /// touch tracking and the hover diff. Everything a pass produces is queued and runs after it as a pass of its own,
    /// so nothing is delivered in the middle of another delivery. Queries are the one exception (§4.3).
    /// </summary>
    /// <remarks>
    /// Created lazily by the root's first notification. It never calls behaviors itself: the nodes forward,
    /// and behaviors are reached as VL behaviors. Before every notification from outside, and when the root renders,
    /// it sweeps the pressed pointers and the hover path, so nodes that left the tree get their cancels and leaves.
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
        private readonly PointerClaims _claims = new();
        private readonly List<PendingNotification> _pending = new();
        private readonly List<IInteractive> _previousHover = new();
        private readonly List<IInteractive> _hoverBuffer = new();

        private TimeProvider _clock = TimeProvider.System;
        private long _origin = TimeProvider.System.GetTimestamp();
        private CallerInfo _lastCaller = CallerInfo.Default;
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
        /// The pressed pointers of this root and their claims.
        /// </summary>
        public PointerClaims Claims => _claims;

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
        /// Otherwise it sweeps first, so the cancels and leaves of nodes that left the tree run before the notification.
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
                _lastCaller = caller;
                SweepIntoOutbox();
                Drain();

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
        /// Delivers the cancels and leaves that nodes which left the tree, or behaviors removed from their nodes, are owed
        /// (v2 §5.4 rows 5 and 6, §3.5). Called by the root when it renders; does nothing while a pass is running.
        /// </summary>
        public void Sweep()
        {
            if (_running)
                return;

            _running = true;
            try
            {
                SweepIntoOutbox();
                Drain();
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
                if (TryBuildRoute(pass, new Delivery(DeliveryClass.FanOut, RouteKind.Subtree, outer.Node), out _))
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

            // A device loss, or a touch going down with the id of one whose Up was lost, ends the presses it makes obsolete first.
            _claims.Prelude(notification, _pending);
            if (_pending.Count > 0)
            {
                var prelude = _pending.ToArray();
                _pending.Clear();
                foreach (var item in prelude)
                    RunPass(item.Notification, caller, item.Delivery);
            }

            var pass = Rent();
            try
            {
                pass.Begin(this, notification, caller, delivery.Class, isQuery: false, now);
                var reached = TryBuildRoute(pass, delivery, out var choice);
                var handled = reached && _root.Forward(pass);

                _touch.Track(notification, now);

                _claims.AfterPass(pass, choice, reached, handled, _pending);
                EnqueuePending(caller);

                UpdateHover(pass, delivery, choice, reached);

                // A touch that reached the tree counts as processed, so Windows doesn't promote it to mouse input (v1 TP).
                return handled || (reached && notification is TouchNotification);
            }
            finally
            {
                _pending.Clear();
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

        private void EnqueuePending(CallerInfo caller)
        {
            foreach (var item in _pending)
                Enqueue(item.Notification, caller, item.Delivery);

            _pending.Clear();
        }

        /// <summary>
        /// Queues the cancels and leaves owed to nodes that left the tree, with the caller of the last notification from outside.
        /// </summary>
        private void SweepIntoOutbox()
        {
            _claims.Sweep(_root, _pending);
            SweepHover();
            EnqueuePending(_lastCaller);
        }

        /// <summary>
        /// Cuts the hover path where it stops being reachable. The nodes cut off get their leave, deepest first,
        /// along the path as it was, so a node that left the tree gets it too.
        /// </summary>
        private void SweepHover()
        {
            var path = _hover.Path;
            if (path.Count == 0)
                return;

            var valid = PointerClaims.ValidPrefix(_root, path);
            for (var i = path.Count - 1; i >= valid; i--)
            {
                _pending.Add(
                    new PendingNotification(
                        new PointerLeaveNotification(_hover.PointerId, Keys.None, null!),
                        Delivery.Along(path, i + 1, i)
                    )
                );
            }

            _hover.Truncate(valid);
        }

        /// <summary>
        /// Builds the route of <paramref name="delivery"/> into the pass's path list, as the tree is now (rule 4).
        /// False when it reaches nothing: no node is hit, the node it names isn't in this root's tree anymore,
        /// or the path a pointer was pressed on is empty.
        /// </summary>
        /// <param name="choice">For pointer routes, which route was chosen; <see cref="PointerRouteChoice.Hit"/> otherwise.</param>
        private bool TryBuildRoute(InteractionPass pass, Delivery delivery, out PointerRouteChoice choice)
        {
            var path = pass.PathBuffer;
            path.Clear();
            choice = PointerRouteChoice.Hit;

            switch (delivery.Route)
            {
                case RouteKind.Pointer:
                    choice = _claims.Choose(pass.Notification, out var claimNode, out var recorded);
                    switch (choice)
                    {
                        case PointerRouteChoice.Claim:
                            if (!NotificationRoute.TryFillPath(_root, claimNode!, path))
                                return false;

                            pass.SetRoute(NotificationRoute.Target(path));
                            return true;

                        case PointerRouteChoice.Recorded:
                            path.AddRange(recorded!);
                            if (path.Count == 0)
                                return false;

                            pass.SetRoute(NotificationRoute.Hit(path));
                            return true;

                        default:
                            return TryBuildHitRoute(pass, path);
                    }

                case RouteKind.Hit:
                    return TryBuildHitRoute(pass, path);

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

                case RouteKind.Snapshot:
                    if (delivery.Path is not { Count: > 0 } snapshot)
                        return false;

                    path.AddRange(snapshot);
                    pass.SetRoute(NotificationRoute.Snapshot(path, delivery.From), delivery.Direct);
                    return true;

                default:
                    path.Add(_root);
                    pass.SetRoute(NotificationRoute.Broadcast(path));
                    return true;
            }
        }

        private bool TryBuildHitRoute(InteractionPass pass, List<IInteractive> path)
        {
            if (
                pass.Notification is not INotificationWithPosition positioned
                || !_root.TryBuildHitPath(positioned.Position, pass.Caller.Transformation, path)
            )
                return false;

            pass.SetRoute(NotificationRoute.Hit(path));
            return true;
        }

        /// <summary>
        /// Moves the hover path after a pointer pass and queues the leaves and enters it causes (v2 §3.5).
        /// A claimed pointer hovers its claim node (G3); otherwise the mouse and the primary touch hover their hit path,
        /// which is tested again when the input followed a recorded path. A lifted touch and a lost mouse hover nothing.
        /// </summary>
        private void UpdateHover(InteractionPass pass, Delivery delivery, PointerRouteChoice choice, bool reached)
        {
            var notification = pass.Notification;
            IReadOnlyList<IInteractive> next;
            int pointerId;

            switch (notification)
            {
                case MouseLostNotification:
                    next = Array.Empty<IInteractive>();
                    pointerId = PointerIds.Mouse;
                    break;
                case MouseNotification when delivery.Class == DeliveryClass.Input:
                    pointerId = PointerIds.Mouse;
                    next = HoverTarget(pass, delivery, pointerId, choice, reached);
                    break;
                case TouchNotification { Primary: true } touch when delivery.Class == DeliveryClass.Input:
                    pointerId = touch.Id;
                    next =
                        touch.Kind == TouchNotificationKind.TouchUp
                            ? Array.Empty<IInteractive>()
                            : HoverTarget(pass, delivery, pointerId, choice, reached);
                    break;
                default:
                    return;
            }

            var common = _hover.Update(next, pointerId, _previousHover);
            var modifiers = notification is NotificationBase withKeys ? withKeys.ModifierKeys : Keys.None;
            var sender = notification.Sender;

            for (var i = _previousHover.Count - 1; i >= common; i--)
            {
                Enqueue(
                    new PointerLeaveNotification(pointerId, modifiers, sender),
                    pass.Caller,
                    Delivery.Along(_previousHover, i + 1, i)
                );
            }

            var path = _hover.Path;
            for (var i = common; i < path.Count; i++)
            {
                Enqueue(
                    new PointerEnterNotification(pointerId, modifiers, sender),
                    pass.Caller,
                    new Delivery(DeliveryClass.Control, RouteKind.Target, path[i])
                );
            }

            _previousHover.Clear();
            _hoverBuffer.Clear();
        }

        private IReadOnlyList<IInteractive> HoverTarget(
            InteractionPass pass,
            Delivery delivery,
            int pointerId,
            PointerRouteChoice choice,
            bool reached
        )
        {
            if (_claims.ClaimOf(pointerId) is { } claim)
            {
                return NotificationRoute.TryFillPath(_root, claim.Node, _hoverBuffer)
                    ? _hoverBuffer
                    : Array.Empty<IInteractive>();
            }

            var freshHit = delivery.Route == RouteKind.Hit || (delivery.Route == RouteKind.Pointer && choice == PointerRouteChoice.Hit);
            if (freshHit)
                return reached ? pass.Route.Path : Array.Empty<IInteractive>();

            _hoverBuffer.Clear();
            return pass.Notification is INotificationWithPosition positioned
                && _root.TryBuildHitPath(positioned.Position, pass.Caller.Transformation, _hoverBuffer)
                ? _hoverBuffer
                : Array.Empty<IInteractive>();
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
