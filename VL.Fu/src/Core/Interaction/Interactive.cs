using Microsoft.Extensions.Logging;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Layout;
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
    /// A node is a VL layer; it forwards notifications to its children, its layer and its behaviors.
    /// </summary>
    public interface IInteractive : ILayoutable
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

    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Interactive<TSelf> : Layoutable<TSelf>, IInteractive
        where TSelf : class, ITraversable<TSelf>, IInteractive
    {
        private PointerEvents _pointerEvents = PointerEvents.Auto;
        private PointerEvents _styledPointerEvents = PointerEvents.Auto;
        private PointerEvents _committedPointerEvents = PointerEvents.Auto;

        private Spread<IBehavior> _behaviors = Spread<IBehavior>.Empty;

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

            _behaviors = SanitizeBehaviors(behaviors);
        }

        /// <summary>
        /// The children in draw order, back to front.
        /// Rendering walks it forward and hit testing will walk it backward, so the front-most child is asked first.
        /// </summary>
        /// <remarks>
        /// Tree order for now; the z-index sort (task 05) changes only this accessor.
        /// </remarks>
        protected IReadOnlyList<TSelf> OrderedChildren => Children;

        /// <summary>
        /// Delivers <paramref name="notification"/> to this node and its subtree. Not implemented yet: nothing is delivered
        /// and false is returned, so the notification counts as unprocessed.
        /// </summary>
        public bool Notify(INotification notification, CallerInfo caller)
        {
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
