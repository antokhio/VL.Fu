using SkiaSharp;
using VL.Fu.Core.Interaction;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Behaviors
{
    /// <summary>
    /// One manipulation of a <see cref="SelectionProvider"/>'s selected items as a group, from the start of a gesture to its end
    /// or cancel (v2 §7.2). The members are fixed when it begins, so changing the selection in the middle doesn't add or drop
    /// anyone, and every member that began gets its end.
    /// </summary>
    /// <remarks>
    /// The behavior holding the pointer drives it: an item's <see cref="Movable"/> or <see cref="Transformable"/>, or the provider itself
    /// for a drag inside its selection bounds. A provider runs one transaction at a time.
    /// </remarks>
    public sealed class GroupTransaction
    {
        private readonly SelectionProvider _provider;
        private readonly IInteractive _providerNode;
        private readonly object _driver;
        private readonly int _pointerId;
        private readonly Dictionary<object, Space> _members = new(ReferenceEqualityComparer.Instance);

        private bool _cancelled = false;
        private bool _closed = false;

        internal GroupTransaction(SelectionProvider provider, IInteractive providerNode, object driver, int pointerId)
        {
            _provider = provider;
            _providerNode = providerNode;
            _driver = driver;
            _pointerId = pointerId;
        }

        public SelectionProvider Provider => _provider;

        /// <summary>
        /// Whether <paramref name="owner"/> joined when the transaction began.
        /// </summary>
        public bool IsMember(object owner) => owner is not null && _members.ContainsKey(owner);

        /// <summary>
        /// The provider was disabled while the transaction ran. The driver sends <see cref="GroupPhase.Cancel"/>
        /// with the next notification of its pointer.
        /// </summary>
        internal bool IsCancelled => _cancelled;

        internal object Driver => _driver;

        internal int PointerId => _pointerId;

        internal bool IsClosed => _closed;

        internal void MarkCancelled() => _cancelled = true;

        /// <summary>
        /// Asks for the group <paramref name="node"/> belongs to, for a gesture of <paramref name="driver"/> on <paramref name="pointerId"/>.
        /// Returns false when that group is manipulated already, and the gesture should do nothing. Otherwise
        /// <paramref name="transaction"/> is the new transaction, or null when the node isn't selected under an enabled provider
        /// and moves alone.
        /// </summary>
        internal static bool TryStart(object driver, int pointerId, IInteractive node, out GroupTransaction? transaction)
        {
            transaction = null;
            if (
                !Selectable.IsSelectedOn(node)
                || !SelectionProvider.TryFind(node, out var provider, out var providerNode)
                || !provider.Enabled
            )
                return true;

            if (!provider.TryBeginGroup(driver, pointerId, providerNode, out var started))
                return false;

            transaction = started;
            return true;
        }

        internal void Add(object owner, SKMatrix windowToParent, SKMatrix parentToWindow) =>
            _members[owner] = new Space(windowToParent, parentToWindow);

        internal bool TryGetSpace(object owner, out SKMatrix windowToParent, out SKMatrix parentToWindow)
        {
            if (owner is null || !_members.TryGetValue(owner, out var space))
            {
                windowToParent = parentToWindow = SKMatrix.Identity;
                return false;
            }

            windowToParent = space.WindowToParent;
            parentToWindow = space.ParentToWindow;
            return true;
        }

        /// <summary>
        /// Lets the selected items join, as displayed nodes of the provider's subtree.
        /// </summary>
        internal void Begin(INotification notification) =>
            Send(notification, GroupPhase.Begin, SKMatrix.Identity, DeliveryClass.FanOut);

        /// <summary>
        /// Sends the change since the start of the gesture, in window pixels, to every member.
        /// </summary>
        internal void Apply(INotification notification, in SKMatrix deltaInWindow)
        {
            if (!_closed)
                Send(notification, GroupPhase.Apply, deltaInWindow, DeliveryClass.Control);
        }

        internal void End(INotification notification) => Close(notification, GroupPhase.End);

        internal void Cancel(INotification notification) => Close(notification, GroupPhase.Cancel);

        /// <summary>
        /// Sends the last phase and frees the provider for the next transaction, even when the phase can't be sent.
        /// </summary>
        private void Close(INotification notification, GroupPhase phase)
        {
            if (_closed)
                return;

            _closed = true;
            Send(notification, phase, SKMatrix.Identity, DeliveryClass.Control);
            _provider.EndGroup(this);
        }

        // One pass into the provider's subtree per phase: members filter themselves, so a big selection costs one pass (D2).
        private void Send(INotification notification, GroupPhase phase, SKMatrix deltaInWindow, DeliveryClass @class)
        {
            var modifiers = (notification as NotificationBase)?.ModifierKeys ?? Keys.None;
            notification.DispatchInto(
                _providerNode,
                new GroupManipulationNotification(this, phase, deltaInWindow, modifiers),
                @class
            );
        }

        private readonly record struct Space(SKMatrix WindowToParent, SKMatrix ParentToWindow);
    }
}
