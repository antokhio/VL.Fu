using Microsoft.Extensions.Logging;
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Layout;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Tree;
using VL.Lib.Collections;
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
