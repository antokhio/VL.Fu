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
    }

    /// <summary>
    /// The plan for delivering one notification: its class, the kind of route, and the node a target or subtree route names.
    /// </summary>
    internal readonly record struct Delivery(DeliveryClass Class, RouteKind Route, IInteractive? Node = null);
}
