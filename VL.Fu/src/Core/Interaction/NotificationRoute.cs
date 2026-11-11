using VL.Fu.Core.Tree;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// The nodes a notification travels through: a path from the root down, and what happens on and after it.
    /// A node on the path forwards only to the next one; at the end of the path the route may descend into the whole subtree.
    /// </summary>
    internal sealed class NotificationRoute
    {
        private readonly IReadOnlyList<IInteractive> _path;
        private readonly bool _bubbles;
        private readonly bool _descends;

        private NotificationRoute(IReadOnlyList<IInteractive> path, bool bubbles, bool descends)
        {
            _path = path;
            _bubbles = bubbles;
            _descends = descends;
        }

        /// <summary>
        /// The nodes from the root down to the last one the route names. Never empty.
        /// </summary>
        public IReadOnlyList<IInteractive> Path => _path;

        /// <summary>
        /// The whole tree below <paramref name="root"/>, front to back, like a VL group.
        /// </summary>
        public static NotificationRoute Broadcast(IInteractive root) =>
            new(new[] { root }, bubbles: true, descends: true);

        /// <summary>
        /// The hit path from the root down to the front-most node hit. Every node on it receives the notification,
        /// the deepest first, so it bubbles up to the ancestors.
        /// </summary>
        public static NotificationRoute Hit(IReadOnlyList<IInteractive> path) =>
            new(path, bubbles: true, descends: false);

        /// <summary>
        /// Only <paramref name="node"/> receives the notification. Its ancestors forward it without receiving it,
        /// and its children don't get it.
        /// </summary>
        public static NotificationRoute Target(IInteractive node) =>
            new(PathTo(node), bubbles: false, descends: false);

        /// <summary>
        /// Whether the node at <paramref name="depth"/> hands the notification to its own layer and behaviors:
        /// the last node of the path and everything below it always do, the nodes above it only when the route bubbles.
        /// </summary>
        public bool Receives(int depth) => depth >= _path.Count - 1 || _bubbles;

        /// <summary>
        /// Whether the node at <paramref name="depth"/> forwards to all of its children rather than to the next path node.
        /// </summary>
        public bool Descends(int depth) => depth >= _path.Count - 1 && _descends;

        /// <summary>
        /// The node the path continues with below <paramref name="depth"/>, or null at its end.
        /// </summary>
        public IInteractive? Next(int depth) => depth + 1 < _path.Count ? _path[depth + 1] : null;

        private static IInteractive[] PathTo(IInteractive node)
        {
            var path = new List<IInteractive>();
            for (ITraversable? current = node; current is IInteractive interactive; current = current.Parent)
                path.Add(interactive);

            path.Reverse();
            return path.ToArray();
        }
    }
}
