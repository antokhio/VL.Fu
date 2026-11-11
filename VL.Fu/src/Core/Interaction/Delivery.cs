using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// How a notification is delivered along its route: which nodes are gated out, and whether a true stops it.
    /// </summary>
    internal enum DeliveryClass
    {
        /// <summary>
        /// Pointer, key and custom input, with VL semantics: a node that isn't drawn is skipped with its subtree,
        /// a child or a <c>Layer</c> returning true ends the node, and the node returns true if anyone processed it.
        /// </summary>
        Input,

        /// <summary>
        /// Device loss, cancels, enter and leave: every node on the route is reached, drawn or not,
        /// and whatever a receiver returns is ignored, so nothing can block it.
        /// </summary>
        Control,

        /// <summary>
        /// Notifications a node sends into its subtree: nodes that aren't drawn are skipped,
        /// and every other node is reached whatever a receiver returns.
        /// </summary>
        FanOut,
    }

    /// <summary>
    /// The kind of route a notification takes. The route itself is built when its pass starts,
    /// so a queued notification sees the tree as it is then.
    /// </summary>
    internal enum RouteKind
    {
        /// <summary>
        /// The whole tree, front to back, like a VL group.
        /// </summary>
        Broadcast,

        /// <summary>
        /// The nodes under the notification's position, from the root down to the front-most one hit.
        /// </summary>
        Hit,

        /// <summary>
        /// One node. Its ancestors only forward, and its children don't get it.
        /// </summary>
        Target,

        /// <summary>
        /// A node and its whole subtree. Its ancestors only forward.
        /// </summary>
        Subtree,

        /// <summary>
        /// Pointer input. Chosen when the pass starts from the pointer's state: the claim node when the pointer is claimed,
        /// the path it was pressed on while it is pressed, otherwise the nodes under its position.
        /// </summary>
        Pointer,

        /// <summary>
        /// A copy of a path taken when the notification was queued, walked as it was recorded: a node that left the tree
        /// since still receives it. The nodes from <see cref="Delivery.From"/> down receive it, the ones above only forward.
        /// </summary>
        Snapshot,
    }

    /// <summary>
    /// The plan for delivering one notification: its class, the kind of route, and the node a target or subtree route names.
    /// </summary>
    /// <param name="Path">The copied path of a snapshot route, root first.</param>
    /// <param name="From">The first depth of <paramref name="Path"/> that receives the notification.</param>
    /// <param name="Direct">
    /// A behavior that is no longer attached to the last node of <paramref name="Path"/>. It alone receives the notification there,
    /// instead of the node's layer and behaviors.
    /// </param>
    internal readonly record struct Delivery(
        DeliveryClass Class,
        RouteKind Route,
        IInteractive? Node = null,
        IReadOnlyList<IInteractive>? Path = null,
        int From = 0,
        IBehavior? Direct = null
    )
    {
        /// <summary>
        /// A control delivery along a copy of the first <paramref name="count"/> nodes of <paramref name="path"/>,
        /// received from depth <paramref name="from"/> down.
        /// </summary>
        public static Delivery Along(IReadOnlyList<IInteractive> path, int count, int from, IBehavior? direct = null)
        {
            var copy = new IInteractive[count];
            for (var i = 0; i < count; i++)
                copy[i] = path[i];

            return new Delivery(DeliveryClass.Control, RouteKind.Snapshot, Path: copy, From: from, Direct: direct);
        }
    }

    /// <summary>
    /// A notification someone produced during or before a pass, with how it is to be delivered. The dispatcher adds the caller.
    /// </summary>
    internal readonly record struct PendingNotification(INotification Notification, Delivery Delivery);
}
