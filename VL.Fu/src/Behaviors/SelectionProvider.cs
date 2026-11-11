using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Fu.Core.Tree;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Behaviors
{
    /// <summary>
    /// How many items of a <see cref="SelectionProvider"/> the user can select.
    /// </summary>
    public enum SelectionMode
    {
        /// <summary>
        /// One item at a time: Shift and Ctrl replace the selection like a plain press, and there is no marquee.
        /// </summary>
        Single,

        /// <summary>
        /// Any number of items: Shift adds, Ctrl toggles, and dragging on the background draws a marquee.
        /// </summary>
        Multi,
    }

    /// <summary>
    /// Makes the node it is attached to a selection scope: the <see cref="Selectable"/> items below it select together,
    /// and its own background clears the selection on a click and draws a marquee on a drag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The provider stores no selection: each item's <c>IsSelected</c> channel is the truth. The provider changes it by sending
    /// a <see cref="SelectionNotification"/> into its subtree, and items of a nested provider ignore it.
    /// </para>
    /// <para>
    /// A drag on the background that starts outside <see cref="SelectionBounds"/> draws a marquee. It asks for the pointer when
    /// it passes the threshold and, once granted, writes <see cref="MarqueeRect"/>. On release the items the marquee fully contains
    /// are selected, and <see cref="MarqueeRect"/> returns to no value. A cancelled marquee selects nothing.
    /// A drag that starts inside <see cref="SelectionBounds"/> is reserved for moving the selection and does nothing yet.
    /// </para>
    /// <para>
    /// <see cref="SelectionBounds"/> is refreshed with every pointer input the provider receives and after every change it makes.
    /// A change the patch makes through an item's channel shows up with the next pointer input.
    /// </para>
    /// </remarks>
    [ProcessNode(Name = "SelectionProvider", HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public sealed class SelectionProvider : NodeBase, IBehavior
    {
        private readonly ClickGestureRecognizer _click = new();
        private readonly DragGestureRecognizer _drag = new();

        private IChannel<Optional<RectangleF>>? _selectionBounds = null;
        private IChannel<Optional<RectangleF>>? _marqueeRect = null;
        private SelectionMode _mode = SelectionMode.Multi;
        private bool _marqueeEnabled = true;
        private bool _enabled = true;

        private bool _granted = false;
        private int? _owedRelease = null;
        private SKMatrix _windowToNodeAtPress = SKMatrix.Identity;
        private Vector2 _marqueeStart = Vector2.Zero;
        private Vector2 _lastInWindow = Vector2.Zero;
        private INotification? _itemPress = null;
        private int _itemPressPointerId = 0;

        [Fragment]
        public SelectionProvider([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// The bounds of the selected items in this node's space, or no value while nothing is selected.
        /// </summary>
        public IChannel<Optional<RectangleF>>? SelectionBounds => _selectionBounds;

        /// <summary>
        /// Connects the channel that receives the selection bounds. They are written with the next pointer input or selection change.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetSelectionBounds(IChannel<Optional<RectangleF>>? selectionBounds)
        {
            if (ReferenceEquals(selectionBounds, _selectionBounds))
                return;

            _selectionBounds = selectionBounds;
        }

        /// <summary>
        /// The marquee being drawn, in this node's space, or no value while there is none.
        /// </summary>
        public IChannel<Optional<RectangleF>>? MarqueeRect => _marqueeRect;

        /// <summary>
        /// Connects the channel that receives the marquee. Changing it while a marquee is drawn cancels the marquee,
        /// which writes no value to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetMarqueeRect(IChannel<Optional<RectangleF>>? marqueeRect)
        {
            if (ReferenceEquals(marqueeRect, _marqueeRect))
                return;

            CancelMarquee();
            _marqueeRect = marqueeRect;
        }

        public SelectionMode Mode => _mode;

        /// <summary>
        /// Sets how many items the user can select. Switching to <see cref="SelectionMode.Single"/> cancels a marquee and keeps
        /// the current selection: only the next user interaction reduces it to one item.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetMode(SelectionMode mode = SelectionMode.Multi)
        {
            if (mode == _mode)
                return;

            if (!Enum.IsDefined(mode))
            {
                this.LogWarning($"Mode can't be {mode}. It is ignored.");
                return;
            }

            if (mode == SelectionMode.Single)
                CancelMarquee();

            _mode = mode;
        }

        /// <summary>
        /// Whether a drag on the background draws a marquee. Only in <see cref="SelectionMode.Multi"/>.
        /// </summary>
        public bool MarqueeEnabled => _marqueeEnabled;

        /// <summary>
        /// The buttons that draw a marquee; a touch counts as <see cref="MouseButtons.Left"/>.
        /// </summary>
        public MouseButtons MarqueeButtons => _drag.Buttons;

        /// <summary>
        /// Turns the marquee on or off and sets its buttons, which apply from the next press. Turning it off cancels a marquee.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetMarquee(bool enabled = true, MouseButtons buttons = MouseButtons.Left)
        {
            if (enabled == _marqueeEnabled && buttons == _drag.Buttons)
                return;

            if (enabled && buttons == MouseButtons.None)
                this.LogWarning("The marquee buttons are None, so no marquee can be drawn.");

            if (!enabled)
                CancelMarquee();

            _marqueeEnabled = enabled;
            _drag.SetButtons(buttons);
        }

        public bool Enabled => _enabled;

        /// <summary>
        /// Disabling cancels a marquee at once and turns selection off for the items in this provider's scope.
        /// They don't fall through to a provider further up.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            if (enabled == _enabled)
                return;

            if (!enabled)
            {
                _click.Cancel();
                CancelMarquee();
                _itemPress = null;
            }

            _enabled = enabled;
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            ReleaseOwedClaim(notification);

            if (!_enabled)
                return false;

            switch (notification)
            {
                case SelectionNotification selection:
                    // Its own change reaches it after its items, so the bounds are up to date.
                    if (ReferenceEquals(selection.Provider, this) && selection.Action != SelectionAction.CollectBounds)
                        RefreshBounds(notification);
                    return false;
                case PointerCaptureNotification capture when capture.Grants(this):
                    return OnGranted(capture);
                case PointerCancelNotification cancel:
                    OnCancelled(cancel);
                    return false;
            }

            if (!PointerInput.TryRead(notification, out var sample))
                return false;

            var isDown = sample.Kind == PointerSampleKind.Down;
            if (isDown && TakeItemPress(in sample))
                return false;

            var bounds = RefreshBounds(notification);
            _lastInWindow = sample.PositionInWindow;

            var click = _click.Process(in sample);
            if (click.Phase == ClickPhase.Clicked)
            {
                notification.Dispatch(
                    new SelectionNotification(this, SelectionAction.Clear, null, default, null, sample.Modifiers)
                );
                return true;
            }

            if (isDown && !CanStartMarquee(in sample, bounds))
                return false;

            var drag = _drag.Process(in sample);
            switch (drag.Phase)
            {
                case DragPhase.Pressed:
                    _windowToNodeAtPress = sample.WindowToNode!.Value;
                    _marqueeStart = GestureMath.Map(in _windowToNodeAtPress, sample.PositionInWindow);
                    return false;
                case DragPhase.Started:
                    // A drag is no click; ancestors still see this move and may ask for the pointer too.
                    _click.Cancel();
                    notification.RequestPointerCapture(this, GestureKind.Drag);
                    return false;
                case DragPhase.Moved when _granted:
                    _marqueeRect?.EnsureValue(new Optional<RectangleF>(CurrentMarquee()));
                    return true;
                case DragPhase.Ended when _granted:
                    var area = CurrentMarquee();
                    _granted = false;
                    _marqueeRect?.EnsureValue(default);
                    notification.Dispatch(
                        new SelectionNotification(this, SelectionAction.Marquee, null, area, sample.WindowToNode, sample.Modifiers)
                    );
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The nearest provider above <paramref name="item"/>, starting at its parent, and the node it is attached to (S1).
        /// A node that is both an item and a provider belongs to the provider above it.
        /// </summary>
        internal static bool TryFind(IInteractive item, out SelectionProvider provider, out IInteractive providerNode)
        {
            for (var current = item.Parent; current is not null; current = current.Parent)
            {
                if (current is not IInteractive node)
                    continue;

                var behaviors = node.Behaviors;
                for (var i = 0; i < behaviors.Count; i++)
                {
                    if (behaviors[i] is SelectionProvider found)
                    {
                        provider = found;
                        providerNode = node;
                        return true;
                    }
                }
            }

            provider = null!;
            providerNode = null!;
            return false;
        }

        /// <summary>
        /// An item of this provider was pressed in the running delivery. The same press reaches the provider after the item,
        /// and must start neither a click nor a marquee on the background.
        /// </summary>
        internal void NoteItemPress(int pointerId)
        {
            _itemPress = InteractionPass.Current?.Notification;
            _itemPressPointerId = pointerId;
        }

        /// <summary>
        /// Sends <paramref name="action"/> about <paramref name="target"/> into the subtree of <paramref name="providerNode"/>,
        /// on behalf of the item receiving <paramref name="notification"/>.
        /// </summary>
        internal void Send(
            INotification notification,
            IInteractive providerNode,
            SelectionAction action,
            IInteractive target,
            Keys modifiers
        ) =>
            notification.DispatchInto(
                providerNode,
                new SelectionNotification(this, action, target, default, null, modifiers)
            );

        /// <summary>
        /// Whether this press is the one an item noted. Any press consumes the note, so one the provider never saw doesn't linger.
        /// </summary>
        private bool TakeItemPress(in PointerSample sample)
        {
            var itemPress = _itemPress;
            _itemPress = null;
            return itemPress is not null
                && sample.PointerId == _itemPressPointerId
                && ReferenceEquals(itemPress, InteractionPass.Current?.Notification);
        }

        private bool CanStartMarquee(in PointerSample sample, RectangleF? bounds)
        {
            if (!_marqueeEnabled || _mode != SelectionMode.Multi || sample.WindowToNode is null)
                return false;

            return bounds is not { } b || !sample.TryGetPositionInNode(out var position) || !b.Contains(position);
        }

        /// <summary>
        /// Asks the items for their bounds and writes them to <see cref="SelectionBounds"/>. Returns them for the press to test.
        /// </summary>
        private RectangleF? RefreshBounds(INotification notification)
        {
            if (InteractionPass.Current is not { IsQuery: false })
                return null;

            var windowToNode = (notification.Sender as FuNotificationSender)?.WindowToNodeMatrix;
            var modifiers = (notification as NotificationBase)?.ModifierKeys ?? Keys.None;
            var query = new SelectionNotification(this, SelectionAction.CollectBounds, null, default, windowToNode, modifiers);
            notification.Query(query);

            var bounds = query.CollectedBounds;
            _selectionBounds?.EnsureValue(bounds is { } b ? new Optional<RectangleF>(b) : default);
            return bounds;
        }

        private RectangleF CurrentMarquee()
        {
            var current = GestureMath.Map(in _windowToNodeAtPress, _lastInWindow);
            var left = MathF.Min(_marqueeStart.X, current.X);
            var top = MathF.Min(_marqueeStart.Y, current.Y);
            return new RectangleF(
                left,
                top,
                MathF.Max(_marqueeStart.X, current.X) - left,
                MathF.Max(_marqueeStart.Y, current.Y) - top
            );
        }

        private bool OnGranted(PointerCaptureNotification capture)
        {
            if (_drag.Current.Phase is not (DragPhase.Started or DragPhase.Moved))
            {
                // The marquee was cancelled after it asked: the pointer is of no use any more.
                capture.ReleasePointerCapture(this);
                return false;
            }

            _granted = true;
            _marqueeRect?.EnsureValue(new Optional<RectangleF>(CurrentMarquee()));
            return true;
        }

        /// <summary>
        /// Forgets a press of the cancelled pointer. A marquee resets unless it is a member of the claim,
        /// or still pending while a weaker gesture holds the pointer.
        /// </summary>
        private void OnCancelled(PointerCancelNotification cancel)
        {
            if (cancel.IsMember(this))
                return;

            var click = _click.Current;
            if (click.Phase != ClickPhase.Idle && click.PointerId == cancel.PointerId)
                _click.Cancel();

            var drag = _drag.Current;
            if (drag.Phase == DragPhase.Idle || drag.PointerId != cancel.PointerId)
                return;

            if (!_granted && cancel.SparesPending(GestureKind.Drag))
                return;

            // The claim was taken away or ended, so there is nothing to let go.
            Reset(owesRelease: false);
        }

        /// <summary>
        /// Cancels the marquee from a setter, which has no notification to let go of the pointer with.
        /// The release is owed until the next notification of that pointer.
        /// </summary>
        private void CancelMarquee() => Reset(owesRelease: true);

        /// <summary>
        /// Returns the marquee to idle. A marquee that was drawn writes its idle fact: no value. Nothing is selected.
        /// </summary>
        private void Reset(bool owesRelease)
        {
            if (_granted)
            {
                if (owesRelease)
                    _owedRelease = _drag.Current.PointerId;

                _marqueeRect?.EnsureValue(default);
            }

            _granted = false;
            _drag.Cancel();
        }

        private void ReleaseOwedClaim(INotification notification)
        {
            if (_owedRelease is not { } pointerId || !PointerIds.TryGet(notification, out var id) || id != pointerId)
                return;

            _owedRelease = null;
            notification.ReleasePointerCapture(this);
        }
    }
}
