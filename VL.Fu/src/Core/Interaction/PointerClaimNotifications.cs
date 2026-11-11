using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// A claim on a pointer was granted (v2 §5.2). Only the claim node receives it. The owners it grants publish the start
    /// of their gesture when they get it, and not before: requesting doesn't mean winning.
    /// </summary>
    /// <remarks>
    /// Sent after the cancel that resets everyone else on the pointer's path. Every behavior on the claim node sees it,
    /// so each asks <see cref="Grants"/> whether it is meant.
    /// </remarks>
    public sealed class PointerCaptureNotification : NotificationBase
    {
        private readonly int _pointerId;
        private readonly PointerClaim _claim;
        private readonly IReadOnlyList<object> _granted;

        internal PointerCaptureNotification(
            int pointerId,
            PointerClaim claim,
            IReadOnlyList<object> granted,
            Keys modifierKeys,
            object sender
        )
            : base(sender, modifierKeys)
        {
            _pointerId = pointerId;
            _claim = claim;
            _granted = granted;
        }

        /// <summary>
        /// The pointer whose input granted the claim: −1 for the mouse, otherwise the touch id.
        /// </summary>
        public int PointerId => _pointerId;

        public PointerClaim Claim => _claim;

        /// <summary>
        /// Whether this notification grants the claim to <paramref name="owner"/>. An owner that joins a running claim gets
        /// a capture of its own, which doesn't grant it again to the members that had it already.
        /// </summary>
        public bool Grants(object owner)
        {
            for (var i = 0; i < _granted.Count; i++)
            {
                if (ReferenceEquals(_granted[i], owner))
                    return true;
            }

            return false;
        }

        public override INotification WithSender(object sender) =>
            new PointerCaptureNotification(_pointerId, _claim, _granted, ModifierKeys, sender);

        public override INotification Transform(INotificationSpaceTransformer transformer) => this;
    }

    /// <summary>
    /// What a pointer was doing on the receiving node is over (v2 §5.3): every behavior that took part resets, unless it is
    /// a member of <see cref="Claim"/>. Behaviors that published something write their idle facts; the others stay silent.
    /// </summary>
    /// <remarks>
    /// Sent along the path the pointer was pressed on when another claim takes the pointer, after a processed Up,
    /// on a device loss, on Esc that nobody processed, and when the claim node leaves the tree.
    /// Nothing can block it, and nodes that left the tree since get it too.
    /// </remarks>
    public sealed class PointerCancelNotification : NotificationBase
    {
        private readonly int _pointerId;
        private readonly PointerClaim? _claim;

        internal PointerCancelNotification(int pointerId, PointerClaim? claim, Keys modifierKeys, object sender)
            : base(sender, modifierKeys)
        {
            _pointerId = pointerId;
            _claim = claim;
        }

        /// <summary>
        /// −1 for the mouse, otherwise the touch id.
        /// </summary>
        public int PointerId => _pointerId;

        /// <summary>
        /// The claim that holds the pointer now, or ended with the Up that caused the cancel. Null when everyone resets.
        /// </summary>
        /// <remarks>
        /// A behavior compares <see cref="PointerClaim.Node"/> with the node it pressed on: a claim on its own node
        /// means the user is still pressing that node.
        /// </remarks>
        public PointerClaim? Claim => _claim;

        /// <summary>
        /// Whether <paramref name="owner"/> is a member of <see cref="Claim"/> and so keeps going.
        /// </summary>
        public bool IsMember(object owner) => _claim is not null && _claim.HasMember(owner);

        public override INotification WithSender(object sender) =>
            new PointerCancelNotification(_pointerId, _claim, ModifierKeys, sender);

        public override INotification Transform(INotificationSpaceTransformer transformer) => this;
    }
}
