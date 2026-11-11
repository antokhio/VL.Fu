using VL.Fu.Core.Tree;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// The nodes a notification travels through: a path from the root down, and what happens on and after it.
    /// A node on the path forwards only to the next one; at the end of the path the route may descend into the whole subtree.
    /// </summary>
    /// <remarks>
    /// The path list belongs to the running pass, which fills it when it starts and reuses it for the next pass.
    /// </remarks>
    internal readonly struct NotificationRoute
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
        /// The nodes from the root down to the last one the route names. Empty for a route that wasn't built.
        /// </summary>
        public IReadOnlyList<IInteractive> Path => _path ?? Array.Empty<IInteractive>();

        /// <summary>
        /// The whole tree below the root, which is the only node on <paramref name="path"/>, front to back, like a VL group.
        /// </summary>
        public static NotificationRoute Broadcast(IReadOnlyList<IInteractive> path) =>
            new(path, bubbles: true, descends: true);

        /// <summary>
        /// The hit path from the root down to the front-most node hit. Every node on it receives the notification,
        /// the deepest first, so it bubbles up to the ancestors.
        /// </summary>
        public static NotificationRoute Hit(IReadOnlyList<IInteractive> path) =>
            new(path, bubbles: true, descends: false);

        /// <summary>
        /// Only the last node of <paramref name="path"/> receives the notification. Its ancestors forward it without
        /// receiving it, and its children don't get it.
        /// </summary>
        public static NotificationRoute Target(IReadOnlyList<IInteractive> path) =>
            new(path, bubbles: false, descends: false);

        /// <summary>
        /// The last node of <paramref name="path"/> and its whole subtree receive the notification.
        /// Its ancestors forward it without receiving it.
        /// </summary>
        public static NotificationRoute Subtree(IReadOnlyList<IInteractive> path) =>
            new(path, bubbles: false, descends: true);

        /// <summary>
        /// Fills <paramref name="path"/> with the nodes from <paramref name="root"/> down to <paramref name="node"/>.
        /// Returns false, leaving <paramref name="path"/> empty, when <paramref name="node"/> isn't in the tree of <paramref name="root"/>.
        /// </summary>
        public static bool TryFillPath(IInteractive root, IInteractive node, List<IInteractive> path)
        {
            path.Clear();
            for (ITraversable? current = node; current is IInteractive interactive; current = current.Parent)
            {
                path.Add(interactive);
                if (ReferenceEquals(interactive, root))
                {
                    path.Reverse();
                    return true;
                }
            }

            path.Clear();
            return false;
        }

        /// <summary>
        /// Whether the node at <paramref name="depth"/> hands the notification to its own layer and behaviors:
        /// the last node of the path and everything below it always do, the nodes above it only when the route bubbles.
        /// </summary>
        public bool Receives(int depth) => depth >= Path.Count - 1 || _bubbles;

        /// <summary>
        /// Whether the node at <paramref name="depth"/> forwards to all of its children rather than to the next path node.
        /// </summary>
        public bool Descends(int depth) => depth >= Path.Count - 1 && _descends;

        /// <summary>
        /// The node the path continues with below <paramref name="depth"/>, or null at its end.
        /// </summary>
        public IInteractive? Next(int depth) => depth + 1 < Path.Count ? Path[depth + 1] : null;
    }
}
