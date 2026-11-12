using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Core.Extensions;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Behaviors
{
    /// <summary>
    /// What a <see cref="GroupManipulationNotification"/> asks the selected items of a <see cref="GroupTransaction"/> to do.
    /// </summary>
    public enum GroupPhase
    {
        /// <summary>
        /// The manipulation starts: every selected item of the provider joins and remembers its value.
        /// </summary>
        Begin,

        /// <summary>
        /// The members apply the delta, measured from <see cref="Begin"/>, to the values they remembered.
        /// </summary>
        Apply,

        /// <summary>
        /// The manipulation is done: the members keep their values and forget the transaction.
        /// </summary>
        End,

        /// <summary>
        /// The manipulation was cancelled: the members return to the values they remembered.
        /// </summary>
        Cancel,
    }

    /// <summary>
    /// Sent into a <see cref="SelectionProvider"/>'s subtree to move or transform its selected items together (v2 §7.2).
    /// The behavior that holds the pointer drives it; every selected item that joins at <see cref="GroupPhase.Begin"/> follows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delta travels in window pixels, measured from the start of the gesture. Each member reads it in its own parent's space
    /// with <see cref="TryGetDeltaInParent"/>, through the space it joined with, so items under different parents move alike on screen.
    /// </para>
    /// <para>
    /// A patched item takes part like <see cref="Movable"/>: on <see cref="GroupPhase.Begin"/> it checks <see cref="Concerns"/> with its node,
    /// calls <see cref="Join"/> and remembers its value; on <see cref="GroupPhase.Apply"/> it applies the delta to that value; on
    /// <see cref="GroupPhase.Cancel"/> it returns to it. <see cref="GroupPhase.Begin"/> reaches displayed nodes only; the other phases
    /// reach hidden members too.
    /// </para>
    /// </remarks>
    public sealed class GroupManipulationNotification : NotificationBase
    {
        private readonly GroupTransaction _transaction;
        private readonly GroupPhase _phase;
        private readonly SKMatrix _deltaInWindow;

        /// <param name="transaction">The manipulation it belongs to.</param>
        /// <param name="phase">What the members do.</param>
        /// <param name="deltaInWindow">For <see cref="GroupPhase.Apply"/>, the change since the start of the gesture, in window pixels.</param>
        /// <param name="modifierKeys">The modifier keys of the input that caused it.</param>
        internal GroupManipulationNotification(
            GroupTransaction transaction,
            GroupPhase phase,
            SKMatrix deltaInWindow,
            Keys modifierKeys
        )
            : this(transaction, phase, deltaInWindow, modifierKeys, transaction.Provider) { }

        private GroupManipulationNotification(
            GroupTransaction transaction,
            GroupPhase phase,
            SKMatrix deltaInWindow,
            Keys modifierKeys,
            object sender
        )
            : base(sender, modifierKeys)
        {
            _transaction = transaction;
            _phase = phase;
            _deltaInWindow = deltaInWindow;
        }

        public GroupTransaction Transaction => _transaction;

        public GroupPhase Phase => _phase;

        /// <summary>
        /// Whether <paramref name="node"/> takes part in the manipulation that begins: its nearest provider is the transaction's,
        /// and an enabled <see cref="Selectable"/> on it reports it selected. False for every phase but <see cref="GroupPhase.Begin"/>,
        /// so an item selected later stays out.
        /// </summary>
        public bool Concerns(IInteractive node) =>
            _phase == GroupPhase.Begin
            && SelectionProvider.TryFind(node, out var provider, out _)
            && ReferenceEquals(provider, _transaction.Provider)
            && Selectable.IsSelectedOn(node);

        /// <summary>
        /// Makes <paramref name="owner"/>, usually the behavior receiving this notification, a member of the transaction,
        /// with the space of the receiving node's parent as it is now. Only on <see cref="GroupPhase.Begin"/>.
        /// False when that space is scaled to zero, and the owner stays out.
        /// </summary>
        public bool Join(object owner)
        {
            if (
                _phase != GroupPhase.Begin
                || owner is null
                || Sender is not FuNotificationSender { WindowToParentMatrix: { } windowToParent }
                || !windowToParent.TryInvert(out var parentToWindow)
            )
                return false;

            _transaction.Add(owner, windowToParent, parentToWindow);
            return true;
        }

        /// <summary>
        /// The change since the start of the gesture in the parent space <paramref name="owner"/> joined with, to apply to the value
        /// it remembered: a position is mapped through it, a transformation is multiplied by it. Only on <see cref="GroupPhase.Apply"/>
        /// and only for a member.
        /// </summary>
        public bool TryGetDeltaInParent(object owner, out Matrix delta)
        {
            var found = TryGetDelta(owner, out var skDelta);
            delta = found ? skDelta.ToMatrix() : Matrix.Identity;
            return found;
        }

        /// <inheritdoc cref="TryGetDeltaInParent"/>
        internal bool TryGetDelta(object owner, out SKMatrix deltaInParent)
        {
            deltaInParent = SKMatrix.Identity;
            if (_phase != GroupPhase.Apply || !_transaction.TryGetSpace(owner, out var windowToParent, out var parentToWindow))
                return false;

            deltaInParent = GestureMath.Conjugate(in _deltaInWindow, in windowToParent, in parentToWindow);
            return true;
        }

        public override INotification WithSender(object sender) =>
            new GroupManipulationNotification(_transaction, _phase, _deltaInWindow, ModifierKeys, sender);

        public override INotification Transform(INotificationSpaceTransformer transformer) => this;
    }
}
