using System.Reactive;
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
    /// Makes the node it is attached to draggable: one pointer pressed with one of <see cref="Buttons"/> that moves further
    /// than <see cref="Threshold"/>. Attach it to a Fu node's behaviors and connect channels to the outputs you need.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the pointer passes the threshold, the drag asks for the pointer. Only once that is granted, it publishes:
    /// <see cref="IsDragging"/> turns true, <see cref="OnDragStart"/> fires and <see cref="Offset"/> follows the pointer.
    /// A drag of an ancestor or a pinch can win the pointer instead, and then this drag publishes nothing.
    /// </para>
    /// <para>
    /// <see cref="Offset"/> is measured in the space of the node's parent as it was at the press, so it stays right while
    /// the patch moves the node by it. It keeps its last value when the drag ends. When the drag is cancelled instead,
    /// because another gesture took the pointer, the pointer was lost, or the behavior was disabled,
    /// <see cref="Offset"/> returns to zero and <see cref="IsDragging"/> to false, and <see cref="OnDragEnd"/> doesn't fire.
    /// </para>
    /// </remarks>
    [ProcessNode(Name = "Draggable", HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public sealed class Draggable : NodeBase, IBehavior
    {
        private readonly DragGestureRecognizer _drag = new();

        private IChannel<Vector2>? _offset = null;
        private IChannel<bool>? _isDragging = null;
        private IChannel<Unit>? _onDragStart = null;
        private IChannel<Unit>? _onDragEnd = null;
        private bool _enabled = true;

        private bool _granted = false;
        private int? _owedRelease = null;

        [Fragment]
        public Draggable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// How far the pointer moved since the press, in the space of the node's parent as it was at the press.
        /// </summary>
        public IChannel<Vector2>? Offset => _offset;

        /// <summary>
        /// Connects the channel that receives the offset. Changing it during a drag cancels the drag,
        /// which writes zero to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetOffset(IChannel<Vector2>? offset)
        {
            if (ReferenceEquals(offset, _offset))
                return;

            CancelDrag();
            _offset = offset;
        }

        /// <summary>
        /// True from the start of the drag until it ends or is cancelled.
        /// </summary>
        public IChannel<bool>? IsDragging => _isDragging;

        /// <summary>
        /// Connects the channel that receives whether the node is dragged. Changing it during a drag cancels the drag,
        /// which writes false to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetIsDragging(IChannel<bool>? isDragging)
        {
            if (ReferenceEquals(isDragging, _isDragging))
                return;

            CancelDrag();
            _isDragging = isDragging;
        }

        /// <summary>
        /// Fires when the drag gets the pointer.
        /// </summary>
        public IChannel<Unit>? OnDragStart => _onDragStart;

        /// <summary>
        /// Connects the channel that fires when a drag starts. Changing it during a drag cancels the drag.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetOnDragStart(IChannel<Unit>? onDragStart)
        {
            if (ReferenceEquals(onDragStart, _onDragStart))
                return;

            CancelDrag();
            _onDragStart = onDragStart;
        }

        /// <summary>
        /// Fires when the pointer is released after a drag. A cancelled drag doesn't fire it.
        /// </summary>
        public IChannel<Unit>? OnDragEnd => _onDragEnd;

        /// <summary>
        /// Connects the channel that fires when a drag ends. Changing it during a drag cancels the drag.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetOnDragEnd(IChannel<Unit>? onDragEnd)
        {
            if (ReferenceEquals(onDragEnd, _onDragEnd))
                return;

            CancelDrag();
            _onDragEnd = onDragEnd;
        }

        /// <summary>
        /// The buttons that drag; a touch counts as <see cref="MouseButtons.Left"/>.
        /// </summary>
        public MouseButtons Buttons => _drag.Buttons;

        /// <summary>
        /// Sets the buttons that drag. Applies from the next press.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetButtons(MouseButtons buttons = MouseButtons.Left)
        {
            if (buttons == _drag.Buttons)
                return;

            if (buttons == MouseButtons.None)
                this.LogWarning("Buttons is None, so nothing drags this node.");

            _drag.SetButtons(buttons);
        }

        /// <summary>
        /// How far, in DIP, the pointer has to move from where it was pressed before the drag starts.
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
        /// Disabling cancels a drag at once, writing zero to <see cref="Offset"/> and false to <see cref="IsDragging"/>.
        /// The pointer is let go with the next notification of it the node receives.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            if (enabled == _enabled)
                return;

            if (!enabled)
                CancelDrag();

            _enabled = enabled;
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            ReleaseOwedClaim(notification);

            if (!_enabled)
                return false;

            switch (notification)
            {
                case PointerCaptureNotification capture when capture.Grants(this):
                    return OnGranted(capture);
                case PointerCancelNotification cancel:
                    OnCancelled(cancel);
                    return false;
            }

            if (!PointerInput.TryRead(notification, out var sample))
                return false;

            var drag = _drag.Process(in sample);
            switch (drag.Phase)
            {
                case DragPhase.Started:
                    // Ancestors still see this move and may ask for the pointer for a stronger gesture.
                    notification.RequestPointerCapture(this, GestureKind.Drag);
                    return false;
                case DragPhase.Moved when _granted:
                    _offset?.EnsureValue(drag.OffsetInParent);
                    return true;
                case DragPhase.Ended when _granted:
                    _granted = false;
                    _offset?.EnsureValue(drag.OffsetInParent);
                    _isDragging?.EnsureValue(false);
                    _onDragEnd?.EnsureValue(Unit.Default, force: true);
                    return true;
                default:
                    return false;
            }
        }

        private bool OnGranted(PointerCaptureNotification capture)
        {
            var current = _drag.Current;
            if (current.Phase is not (DragPhase.Started or DragPhase.Moved))
            {
                // The drag was cancelled after it asked: the pointer is of no use any more.
                capture.ReleasePointerCapture(this);
                return false;
            }

            _granted = true;
            _offset?.EnsureValue(current.OffsetInParent);
            _isDragging?.EnsureValue(true);
            _onDragStart?.EnsureValue(Unit.Default, force: true);
            return true;
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
            Reset(owesRelease: false);
        }

        /// <summary>
        /// Cancels the drag from a setter, which has no notification to let go of the pointer with.
        /// The release is owed until the next notification of that pointer.
        /// </summary>
        private void CancelDrag() => Reset(owesRelease: true);

        /// <summary>
        /// Returns to idle. A drag that published writes its idle facts (B7): zero offset, not dragging, no end.
        /// </summary>
        private void Reset(bool owesRelease)
        {
            if (_granted)
            {
                if (owesRelease)
                    _owedRelease = _drag.Current.PointerId;

                _offset?.EnsureValue(Vector2.Zero);
                _isDragging?.EnsureValue(false);
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
