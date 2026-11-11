using Microsoft.Extensions.Logging;
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Rendering;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Tree;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// The interaction layer of the element chain: which notifications a node takes part in, and the behaviors attached to it.
    /// With it a node is a full VL.Skia layer: it forwards notifications to its children, its layer and its behaviors.
    /// </summary>
    public interface IInteractive : IRenderable, ILayer
    {
        /// <summary>
        /// Whether hit testing considers this node and its subtree. <see cref="PointerEvents.Auto"/> by default.
        /// </summary>
        /// <remarks>
        /// <see cref="PointerEvents.None"/> lets the pointer reach what is behind the node.
        /// Control notifications, like a cancel of a running gesture, still reach it.
        /// </remarks>
        PointerEvents PointerEvents { get; }
        void SetPointerEvents(PointerEvents pointerEvents);

        /// <summary>
        /// The behaviors attached to this node, in pin order.
        /// Fu's own behaviors and behaviors patched in VL are treated alike.
        /// </summary>
        /// <remarks>
        /// The node doesn't dispose them: the patch that created them owns them.
        /// </remarks>
        IReadOnlyList<IBehavior> Behaviors { get; }

        /// <summary>
        /// Attaches <paramref name="behaviors"/> to this node, replacing the ones attached before. Null attaches none.
        /// </summary>
        /// <remarks>
        /// Empty entries are ignored, and a behavior listed twice is attached once. Fu nodes are ignored too:
        /// every VL layer is also an <see cref="IBehavior"/>, but a node belongs into the children.
        /// All of them are reported in one warning.
        /// </remarks>
        void SetBehaviors(IReadOnlyList<IBehavior>? behaviors);
    }

    /// <summary>
    /// Last generic layer of the element chain: delivers notifications into the tree the way <see cref="Renderable{TSelf}"/> draws it.
    /// <see cref="Region.RegionProvider"/> closes the chain at <see cref="IFuNode"/>, the type every node of a Fu tree has.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Interactive<TSelf> : Renderable<TSelf>, IInteractive, INotificationRoot
        where TSelf : class, ITraversable<TSelf>, IInteractive
    {
        private PointerEvents _pointerEvents = PointerEvents.Auto;
        private PointerEvents _styledPointerEvents = PointerEvents.Auto;
        private PointerEvents _committedPointerEvents = PointerEvents.Auto;

        private Spread<IBehavior> _behaviors = Spread<IBehavior>.Empty;
        private NotificationDispatcher? _dispatcher;

        protected Interactive([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public PointerEvents PointerEvents => _pointerEvents;

        public void SetPointerEvents(PointerEvents pointerEvents)
        {
            if (!PointerEventsStyle.IsSupported(pointerEvents))
            {
                Log(LogLevel.Warning, $"PointerEvents can't be {pointerEvents}. It is ignored.");
                return;
            }

            _pointerEvents = pointerEvents;
        }

        public IReadOnlyList<IBehavior> Behaviors => _behaviors;

        public void SetBehaviors(IReadOnlyList<IBehavior>? behaviors)
        {
            behaviors ??= Spread<IBehavior>.Empty;
            if (SameBehaviors(_behaviors, behaviors))
                return;

            var previous = _behaviors;
            _behaviors = SanitizeBehaviors(behaviors);
            NoteRemovedBehaviors(previous, _behaviors);
        }

        /// <summary>
        /// Runs the notifications this node receives as a root. Created by the first one, and dropped when the node
        /// gets a parent or is disposed, which forgets hover and touch state without sending notifications.
        /// </summary>
        internal NotificationDispatcher Dispatcher => _dispatcher ??= new NotificationDispatcher(this);

        /// <summary>
        /// Delivers <paramref name="notification"/> into the tree the way <see cref="Renderable{TSelf}.Render"/> draws it,
        /// so a Fu tree takes part in VL's notification flow like any layer. Returns true when a receiver processed it.
        /// </summary>
        /// <remarks>
        /// Only the root chooses where a notification goes, so a child passes it on to its root, whose
        /// <see cref="NotificationDispatcher"/> runs it as a pass.
        /// A positional notification goes to the front-most node under its position and bubbles up to its ancestors;
        /// a point outside every node delivers nothing. Device loss reaches every node and is never reported as processed.
        /// Anything else is offered to the whole tree front to back, like a VL group.
        /// A notification sent to the tree while it is delivering another one is queued and runs right after it,
        /// so that call returns false.
        /// </remarks>
        public bool Notify(INotification notification, CallerInfo caller)
        {
            if (Parent is not null)
                return ((TSelf)(object)this).Root().Notify(notification, caller);

            return Dispatcher.Run(notification, caller);
        }

        bool INotificationRoot.Forward(InteractionPass pass) =>
            Forward(pass.Notification, pass.Caller, pass, depth: 0);

        bool INotificationRoot.TryBuildHitPath(Vector2 position, SKMatrix parentToWindow, List<IInteractive> path) =>
            TryBuildHitPath(position, parentToWindow, path);

        bool IInteractionNode.IsHitTestable => HasLayout && IsDisplayed && _pointerEvents != PointerEvents.None;

        /// <summary>
        /// Finds the front-most node under <paramref name="position"/> in this node's subtree and fills <paramref name="path"/>
        /// with the nodes from this one down to it. Returns false, leaving <paramref name="path"/> as it was, when nothing is hit.
        /// </summary>
        /// <param name="position">The notification's position, in the pixel space of <paramref name="parentToWindow"/>.</param>
        /// <param name="parentToWindow">Maps the parent's space, or the caller's for a root, into window pixels.</param>
        /// <param name="path">Receives the hit path, root first.</param>
        /// <remarks>
        /// A node that wasn't laid out, has display none or <see cref="PointerEvents.None"/> is skipped with its subtree,
        /// and so is a node scaled to zero. Children are asked front to back, only inside the clip area when the node clips,
        /// and the first one hit ends the walk, so nothing falls through to the siblings behind it.
        /// Otherwise the node itself is hit inside its box; the right and bottom edges belong to the neighbors.
        /// An ancestor whose box doesn't contain the point is still on the path when a child overflowing it is hit.
        /// </remarks>
        internal bool TryBuildHitPath(Vector2 position, SKMatrix parentToWindow, List<IInteractive> path)
        {
            if (!HasLayout || !IsDisplayed || _pointerEvents == PointerEvents.None)
                return false;

            var nodeToWindow = parentToWindow.PreConcat(LocalTransformation);
            if (!nodeToWindow.TryInvert(out var windowToNode))
                return false;

            var local = windowToNode.MapPoint(position.X, position.Y);
            path.Add(this);

            var children = OrderedChildren;
            if (children.Count > 0 && (ClipArea is not { } clip || Contains(clip, local)))
            {
                for (var i = children.Count - 1; i >= 0; i--)
                {
                    if ((object)children[i] is Interactive<TSelf> child && child.TryBuildHitPath(position, nodeToWindow, path))
                        return true;
                }
            }

            var layout = Layout;
            if (Contains(new RectangleF(0, 0, layout.Width, layout.Height), local))
                return true;

            path.RemoveAt(path.Count - 1);
            return false;
        }

        /// <summary>
        /// Sets the value the running styling pass commits to <see cref="PointerEvents"/>.
        /// Each pass starts at <see cref="PointerEvents.Auto"/>, and the commit writes only when the styled value changed,
        /// so a value set directly is kept until the style changes it.
        /// </summary>
        internal void StagePointerEvents(PointerEvents pointerEvents) =>
            _styledPointerEvents = pointerEvents;

        /// <summary>
        /// Starts the styled pointer events from <see cref="PointerEvents.Auto"/>.
        /// </summary>
        protected override void BeginStyle()
        {
            base.BeginStyle();
            _styledPointerEvents = PointerEvents.Auto;
        }

        /// <summary>
        /// Commits the styled pointer events if they changed since the last pass.
        /// </summary>
        protected override void EndStyle()
        {
            if (_styledPointerEvents != _committedPointerEvents)
            {
                _committedPointerEvents = _styledPointerEvents;
                SetPointerEvents(_committedPointerEvents);
            }

            base.EndStyle();
        }

        /// <summary>
        /// An attached node forgets the dispatcher it ran notifications with as a root, since its root runs them now.
        /// </summary>
        protected override void OnParentChanged(TSelf? oldParent, TSelf? newParent)
        {
            base.OnParentChanged(oldParent, newParent);

            if (newParent is not null)
                _dispatcher = null;
        }

        /// <summary>
        /// Drops the dispatcher, forgetting hover and touch state without sending notifications.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _dispatcher = null;

            base.Dispose(disposing);
        }

        /// <summary>
        /// Sends the cancels and leaves owed to nodes that left the tree, were hidden or stopped taking pointer events
        /// since the last notification, so a claim doesn't outlive its node until the pointer moves again.
        /// </summary>
        protected override void OnRenderingAsRoot()
        {
            base.OnRenderingAsRoot();
            _dispatcher?.Sweep();
        }

        /// <summary>
        /// The body every node runs for a notification, mirroring <see cref="Renderable{TSelf}.Render"/>: route children
        /// front to back, then <see cref="Renderable{TSelf}.Layer"/>, then every behavior, all in this node's own space.
        /// </summary>
        /// <param name="depth">This node's index on the route's path, or larger below the end of the path.</param>
        /// <remarks>
        /// Input and fan-out skip a node that isn't drawn, together with its subtree; control reaches every node.
        /// For input, a child or the layer returning true ends the node with true, and its behaviors don't see it;
        /// every behavior still runs when another one returned true. Control and fan-out visit everything and return false.
        /// Children get the notification as it came in. The layer and behaviors get it with a <see cref="FuNotificationSender"/>,
        /// so its positions are in this node's space; while they run, this node is the pass's acting node.
        /// The tree may change during a pass: a node that left the pass's root when it is reached is skipped with its subtree,
        /// and one that left while its children ran doesn't run its own receivers. A snapshot route doesn't check the tree,
        /// since it reaches nodes that already left it, and a route aimed at one behavior gives the last node's
        /// notification to that behavior alone.
        /// </remarks>
        private bool Forward(INotification notification, CallerInfo caller, InteractionPass pass, int depth)
        {
            var checksTree = pass.Route.ChecksTree;
            if (checksTree && !IsAttachedTo(pass.Root))
                return false;

            var isInput = pass.Class == DeliveryClass.Input;
            if (pass.Class != DeliveryClass.Control && (!HasLayout || !IsDisplayed))
                return false;

            var inner = caller.WithTransformation(caller.Transformation.PreConcat(LocalTransformation));

            if (ForwardToChildren(notification, inner, pass, depth) && isInput)
                return true;

            if (!pass.Route.Receives(depth) || (checksTree && !IsAttachedTo(pass.Root)))
                return false;

            var direct = depth == pass.Route.Path.Count - 1 ? pass.Direct : null;
            var layer = direct is null ? Layer : null;
            var behaviors = _behaviors;
            if (direct is null && layer is null && behaviors.Count == 0)
                return false;

            var wrapped = notification.WithSender(
                new FuNotificationSender(
                    this,
                    inner.Transformation,
                    caller.Transformation,
                    caller.Scaling,
                    notification,
                    pass.TimeMs
                )
            );

            var previous = pass.Enter(this);
            try
            {
                if (direct is not null)
                {
                    direct.Notify(wrapped, inner);
                    return false;
                }

                if (layer is not null && layer.Notify(wrapped, inner) && isInput)
                    return true;

                var processed = false;
                for (var i = 0; i < behaviors.Count; i++)
                    processed |= behaviors[i].Notify(wrapped, inner);

                return isInput && processed;
            }
            finally
            {
                pass.Leave(previous);
            }
        }

        /// <summary>
        /// Forwards to the next node of the path, or to every child, front to back, where the route descends.
        /// Input stops at the first child that returns true, like a VL group.
        /// A child that left this node since the route was built, or while its siblings ran, is skipped.
        /// </summary>
        private bool ForwardToChildren(INotification notification, CallerInfo inner, InteractionPass pass, int depth)
        {
            var route = pass.Route;
            if (route.Next(depth) is { } next)
            {
                return next is Interactive<TSelf> child
                    && (!route.ChecksTree || ReferenceEquals(child.Parent, this))
                    && child.Forward(notification, inner, pass, depth + 1);
            }

            if (!route.Descends(depth))
                return false;

            var isInput = pass.Class == DeliveryClass.Input;
            var processed = false;
            var children = OrderedChildren;
            for (var i = children.Count - 1; i >= 0; i--)
            {
                if ((object)children[i] is not Interactive<TSelf> child || !ReferenceEquals(child.Parent, this))
                    continue;

                if (child.Forward(notification, inner, pass, depth + 1))
                {
                    processed = true;
                    if (isInput)
                        break;
                }
            }

            return processed;
        }

        private bool IsAttachedTo(IInteractive root)
        {
            for (ITraversable? node = this; node is not null; node = node.Parent)
            {
                if (ReferenceEquals(node, root))
                    return true;
            }

            return false;
        }

        private static bool Contains(RectangleF area, SKPoint point) =>
            point.X >= area.X && point.X < area.X + area.Width && point.Y >= area.Y && point.Y < area.Y + area.Height;

        private static bool SameBehaviors(IReadOnlyList<IBehavior> current, IReadOnlyList<IBehavior> next)
        {
            if (ReferenceEquals(current, next))
                return true;

            if (current.Count != next.Count)
                return false;

            for (var i = 0; i < current.Count; i++)
            {
                if (!ReferenceEquals(current[i], next[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Tells the root's claims which behaviors left this node while pointers are pressed, so the next sweep takes them out
        /// of their claims and sends each a cancel of its own (v2 §5.4 row 6). Nothing is noted while no pointer is pressed.
        /// </summary>
        private void NoteRemovedBehaviors(IReadOnlyList<IBehavior> previous, IReadOnlyList<IBehavior> current)
        {
            if (previous.Count == 0)
                return;

            if (
                (object)((TSelf)(object)this).Root() is not Interactive<TSelf> root
                || root._dispatcher is not { Claims.HasPressed: true } dispatcher
            )
                return;

            for (var i = 0; i < previous.Count; i++)
            {
                var behavior = previous[i];
                var kept = false;
                for (var j = 0; j < current.Count && !kept; j++)
                    kept = ReferenceEquals(current[j], behavior);

                if (!kept)
                    dispatcher.Claims.NoteRemoved(this, behavior);
            }
        }

        /// <summary>
        /// Drops empty entries, Fu nodes and repeated instances, keeping the first occurrence, and reports them in one warning.
        /// Returns the input unchanged when there is nothing to drop.
        /// </summary>
        private Spread<IBehavior> SanitizeBehaviors(IReadOnlyList<IBehavior> next)
        {
            var seen = new HashSet<IBehavior>(ReferenceEqualityComparer.Instance);
            SpreadBuilder<IBehavior>? kept = null;
            List<string>? problems = null;

            for (var i = 0; i < next.Count; i++)
            {
                var behavior = next[i];
                var problem =
                    behavior is null ? "Ignored an empty behavior."
                    : behavior is IInteractive
                        ? $"Ignored '{behavior}': a Fu node is a layer, so it goes into the children, not the behaviors."
                    : !seen.Add(behavior) ? $"Ignored '{behavior}': it is already attached to this node."
                    : null;

                if (problem is null)
                {
                    kept?.Add(behavior!);
                    continue;
                }

                if (kept is null)
                {
                    kept = Spread.CreateBuilder<IBehavior>(next.Count);
                    for (var j = 0; j < i; j++)
                        kept.Add(next[j]);
                }

                problems ??= new List<string>();
                if (!problems.Contains(problem))
                    problems.Add(problem);
            }

            if (kept is null)
                return next as Spread<IBehavior> ?? Spread.Create(next.ToArray());

            Log(LogLevel.Warning, string.Join(Environment.NewLine, problems!));
            return kept.ToSpread();
        }
    }
}
