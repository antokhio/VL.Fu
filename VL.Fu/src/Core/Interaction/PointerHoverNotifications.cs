using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// The pointer moved onto a node: the node is now on the hover path of the mouse or the primary touch.
    /// Only that node receives it; its ancestors got their own when the pointer entered them.
    /// </summary>
    /// <remarks>
    /// Sent after the notification that moved the pointer, outermost node first. Nothing can block it,
    /// and hidden nodes get it too. Hover isn't press state: a cancelled gesture doesn't change it.
    /// </remarks>
    public sealed class PointerEnterNotification : NotificationBase
    {
        private readonly int _pointerId;

        /// <param name="pointerId">−1 for the mouse, otherwise the touch id.</param>
        /// <param name="modifierKeys">The modifier keys of the notification that moved the pointer.</param>
        /// <param name="sender">The sender of the notification that moved the pointer.</param>
        public PointerEnterNotification(int pointerId, Keys modifierKeys, object sender)
            : base(sender, modifierKeys)
        {
            _pointerId = pointerId;
        }

        /// <summary>
        /// −1 for the mouse, otherwise the id of the touch.
        /// </summary>
        public int PointerId => _pointerId;

        public override INotification WithSender(object sender) => new PointerEnterNotification(_pointerId, ModifierKeys, sender);

        public override INotification Transform(INotificationSpaceTransformer transformer) => this;
    }

    /// <summary>
    /// The pointer left a node: the node is no longer on the hover path of the mouse or the primary touch.
    /// Only that node receives it; its ancestors get their own when the pointer leaves them.
    /// </summary>
    /// <remarks>
    /// Sent after the notification that moved the pointer, deepest node first. Nothing can block it,
    /// and a node that was hidden while hovered gets it too. A lifted touch and a lost mouse leave every node.
    /// </remarks>
    public sealed class PointerLeaveNotification : NotificationBase
    {
        private readonly int _pointerId;

        /// <param name="pointerId">−1 for the mouse, otherwise the touch id.</param>
        /// <param name="modifierKeys">The modifier keys of the notification that moved the pointer.</param>
        /// <param name="sender">The sender of the notification that moved the pointer.</param>
        public PointerLeaveNotification(int pointerId, Keys modifierKeys, object sender)
            : base(sender, modifierKeys)
        {
            _pointerId = pointerId;
        }

        /// <summary>
        /// −1 for the mouse, otherwise the id of the touch.
        /// </summary>
        public int PointerId => _pointerId;

        public override INotification WithSender(object sender) => new PointerLeaveNotification(_pointerId, ModifierKeys, sender);

        public override INotification Transform(INotificationSpaceTransformer transformer) => this;
    }
}
