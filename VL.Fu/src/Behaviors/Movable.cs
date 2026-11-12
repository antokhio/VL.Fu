using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Behaviors
{
    /// <summary>
    /// Lets the user move the node it is attached to by dragging it. Connect a channel to <see cref="Position"/> and apply it to the node,
    /// for example as its position or translation: the drag writes it, and the patch may write it too.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The drag starts when one pointer pressed with one of <see cref="Buttons"/> moves further than <see cref="Threshold"/>, and once it
    /// is granted the pointer, <see cref="Position"/> follows: the position at the press plus how far the pointer moved, in the space
    /// of the node's parent as it was at the press. So the node stays under the pointer.
    /// </para>
    /// <para>
    /// When the node is selected under an enabled <see cref="SelectionProvider"/>, dragging it moves every selected item of that provider
    /// together, and a drag of another selected item or inside the provider's selection bounds moves this one along. A pinch on a
    /// selected <see cref="Transformable"/> maps the position with the group, so items spread apart when the group grows.
    /// </para>
    /// <para>
    /// When the drag is cancelled, because another gesture took the pointer, Esc was pressed, the pointer was lost or the behavior
    /// was disabled, <see cref="Position"/> returns to where it was at the press. Use either <see cref="Movable"/> or
    /// <see cref="Transformable"/> on a node, not both.
    /// </para>
    /// </remarks>
    [ProcessNode(Name = "Movable", HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public sealed class Movable : NodeBase, IBehavior
    {
        private readonly DragGestureRecognizer _drag = new();

        private IChannel<Vector2>? _position = null;
        private bool _enabled = true;

        private bool _granted = false;
        private int? _owedRelease = null;
        private Vector2 _positionAtPress = Vector2.Zero;
        private Vector2 _pressInWindow = Vector2.Zero;
        private Vector2 _lastInWindow = Vector2.Zero;
        private bool _warnedTransformable = false;

        private GroupTransaction? _drive = null;
        private GroupTransaction? _owedGroupCancel = null;
        private GroupTransaction? _member = null;
        private Vector2 _positionAtBegin = Vector2.Zero;

        [Fragment]
        public Movable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// The node's position in its parent's space, in layout units.
        /// </summary>
        public IChannel<Vector2>? Position => _position;

        /// <summary>
        /// Whether the press found a <see cref="Transformable"/> on the same node and warned about it (M1).
        /// It warns once per behavior.
        /// </summary>
        internal bool WarnedTransformable => _warnedTransformable;

        /// <summary>
        /// Connects the channel that holds the position. Changing it during a drag cancels the drag,
        /// which writes the position at the press to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetPosition(IChannel<Vector2>? position)
        {
            if (ReferenceEquals(position, _position))
                return;

            CancelDrag();
            _member = null;
            _position = position;
        }

        /// <summary>
        /// The buttons that move the node; a touch counts as <see cref="MouseButtons.Left"/>.
        /// </summary>
        public MouseButtons Buttons => _drag.Buttons;

        /// <summary>
        /// Sets the buttons that move the node. Applies from the next press.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetButtons(MouseButtons buttons = MouseButtons.Left)
        {
            if (buttons == _drag.Buttons)
                return;

            if (buttons == MouseButtons.None)
                this.LogWarning("Buttons is None, so nothing moves this node.");

            _drag.SetButtons(buttons);
        }

        /// <summary>
        /// How far, in DIP, the pointer has to move from where it was pressed before the node moves.
        /// </summary>
        public float Threshold => _drag.ThresholdDip;

        /// <summary>
        /// Sets the threshold in DIP. Applies from the next press. A negative or non-finite value is reported and ignored.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetThreshold(float threshold = 4f)
        {
            if (threshold == _drag.ThresholdDip)
                return;

            if (!GestureMath.IsValidThreshold(threshold))
            {
                this.LogWarning($"Threshold must be a finite number of at least 0, got {threshold}. It is ignored.");
                return;
            }

            _drag.SetThresholdDip(threshold);
        }

        public bool Enabled => _enabled;

        /// <summary>
        /// Disabling cancels a drag at once, writing the position at the press, and lets go of the pointer with the next notification
        /// of it the node receives. A disabled node stops following a group move and keeps its position.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            if (enabled == _enabled)
                return;

            if (!enabled)
            {
                CancelDrag();
                _member = null;
            }

            _enabled = enabled;
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            SettleOwed(notification);

            if (!_enabled)
                return false;

            switch (notification)
            {
                case GroupManipulationNotification group:
                    OnGroup(group);
                    return false;
                case PointerCaptureNotification capture when capture.Grants(this):
                    return OnGranted(capture);
                case PointerCancelNotification cancel:
                    OnCancelled(cancel);
                    return false;
            }

            if (!PointerInput.TryRead(notification, out var sample))
                return false;

            if (DropCancelledGroup(notification, sample.PointerId))
                return false;

            var drag = _drag.Process(in sample);
            switch (drag.Phase)
            {
                case DragPhase.Pressed:
                    _positionAtPress = _position?.Value ?? Vector2.Zero;
                    _pressInWindow = sample.PositionInWindow;
                    _lastInWindow = sample.PositionInWindow;
                    WarnIfTransformable(sample.Node);
                    return false;
                case DragPhase.Started:
                    _lastInWindow = sample.PositionInWindow;
                    // Ancestors still see this move and may ask for the pointer for a stronger gesture.
                    notification.RequestPointerCapture(this, GestureKind.Drag);
                    return false;
                case DragPhase.Moved when _granted:
                    _lastInWindow = sample.PositionInWindow;
                    Publish(notification, in drag);
                    return true;
                case DragPhase.Ended when _granted:
                    _lastInWindow = sample.PositionInWindow;
                    _granted = false;
                    Publish(notification, in drag);
                    if (_drive is { } transaction)
                    {
                        _drive = null;
                        transaction.End(notification);
                    }
                    return true;
                default:
                    return false;
            }
        }

        private bool OnGranted(PointerCaptureNotification capture)
        {
            var current = _drag.Current;
            if (
                current.Phase is not (DragPhase.Started or DragPhase.Moved)
                || capture.Sender is not FuNotificationSender { Node: var node }
                || !GroupTransaction.TryStart(this, current.PointerId, node, out var transaction)
            )
            {
                // The drag was cancelled after it asked, or its group is moved already: the pointer is of no use.
                capture.ReleasePointerCapture(this);
                _drag.Cancel();
                return false;
            }

            _granted = true;
            if (transaction is not null)
            {
                _drive = transaction;
                transaction.Begin(capture);
            }

            Publish(capture, in current);
            return true;
        }

        /// <summary>
        /// Alone, the position follows the offset in the parent's space. In a group, the offset is sent in window pixels,
        /// and this node moves as a member like the others.
        /// </summary>
        private void Publish(INotification notification, in DragGestureState drag)
        {
            if (_drive is { } transaction)
            {
                var delta = _lastInWindow - _pressInWindow;
                transaction.Apply(notification, SKMatrix.CreateTranslation(delta.X, delta.Y));
                return;
            }

            _position?.EnsureValue(_positionAtPress + drag.OffsetInParent);
        }

        private void OnGroup(GroupManipulationNotification group)
        {
            switch (group.Phase)
            {
                case GroupPhase.Begin:
                    // A node moved alone by its own drag stays out.
                    if (_granted && _drive is null)
                        return;

                    if (group.Sender is FuNotificationSender { Node: var node } && group.Concerns(node) && group.Join(this))
                    {
                        _member = group.Transaction;
                        _positionAtBegin = _position?.Value ?? Vector2.Zero;
                    }
                    return;
                case GroupPhase.Apply when ReferenceEquals(_member, group.Transaction):
                    if (group.TryGetDelta(this, out var delta))
                    {
                        var moved = delta.MapPoint(_positionAtBegin.X, _positionAtBegin.Y);
                        _position?.EnsureValue(new Vector2(moved.X, moved.Y));
                    }
                    return;
                case GroupPhase.End when ReferenceEquals(_member, group.Transaction):
                    _member = null;
                    return;
                case GroupPhase.Cancel when ReferenceEquals(_member, group.Transaction):
                    _member = null;
                    _position?.EnsureValue(_positionAtBegin);
                    return;
            }
        }

        /// <summary>
        /// Resets unless this drag is a member of the claim, or still pending while a weaker gesture holds the pointer.
        /// </summary>
        private void OnCancelled(PointerCancelNotification cancel)
        {
            var current = _drag.Current;
            if (current.Phase == DragPhase.Idle || current.PointerId != cancel.PointerId || cancel.IsMember(this))
                return;

            if (!_granted && cancel.SparesPending(GestureKind.Drag))
                return;

            // The claim was taken away or ended, so there is nothing to let go.
            Reset(cancel, owesRelease: false);
        }

        /// <summary>
        /// The provider was disabled while this drag moved its group: the group is cancelled with this input of the pointer,
        /// and the drag lets go of it.
        /// </summary>
        private bool DropCancelledGroup(INotification notification, int pointerId)
        {
            if (_drive is not { IsCancelled: true } transaction || _drag.Current.PointerId != pointerId)
                return false;

            _drive = null;
            _granted = false;
            _drag.Cancel();
            transaction.Cancel(notification);
            notification.ReleasePointerCapture(this);
            return true;
        }

        /// <summary>
        /// Cancels the drag from a setter, which has no notification to let go of the pointer with.
        /// The release, and the cancel of a group it drives, are owed until the next notification of that pointer (D4).
        /// </summary>
        private void CancelDrag() => Reset(null, owesRelease: true);

        /// <summary>
        /// Returns to idle. A drag that moved the node alone writes its idle fact: the position at the press.
        /// A group it drives is cancelled with <paramref name="notification"/>, or owed without one.
        /// </summary>
        private void Reset(INotification? notification, bool owesRelease)
        {
            if (_granted)
            {
                if (owesRelease)
                    _owedRelease = _drag.Current.PointerId;

                if (_drive is { } transaction)
                {
                    _drive = null;
                    if (notification is not null)
                        transaction.Cancel(notification);
                    else
                        _owedGroupCancel = transaction;
                }
                else
                {
                    _position?.EnsureValue(_positionAtPress);
                }
            }

            _granted = false;
            _drag.Cancel();
        }

        private void SettleOwed(INotification notification)
        {
            if (_owedRelease is not { } pointerId || !PointerIds.TryGet(notification, out var id) || id != pointerId)
                return;

            _owedRelease = null;
            if (_owedGroupCancel is { } transaction)
            {
                _owedGroupCancel = null;
                transaction.Cancel(notification);
            }

            notification.ReleasePointerCapture(this);
        }

        private void WarnIfTransformable(IInteractive node)
        {
            if (_warnedTransformable)
                return;

            var behaviors = node.Behaviors;
            for (var i = 0; i < behaviors.Count; i++)
            {
                if (behaviors[i] is Transformable)
                {
                    _warnedTransformable = true;
                    this.LogWarning("This node also has a Transformable. Both move it, so use one of them.");
                    return;
                }
            }
        }
    }
}
