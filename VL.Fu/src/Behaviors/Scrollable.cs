using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Fu.Core.Layout;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Behaviors
{
    /// <summary>
    /// Scrolls the content of the node it is attached to: the wheel scrolls it, and a touch drags it.
    /// Connect a channel to <see cref="Offset"/> and apply its negation as a translation to the content node's transformation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The content is the node's first child. The offset stays in the range that keeps the content's box covering the node's
    /// padding box, or its whole box when its overflow is visible. The content's box is its layout, which leaves its
    /// transformation out, so an axis along which the content fits doesn't scroll. An offset left outside the range, because
    /// the content shrank, stays where it is and can only move back towards the range.
    /// </para>
    /// <para>
    /// Nested scroll views hand over by themselves: a wheel that doesn't move the offset isn't processed, so it goes on
    /// to the scroll view or canvas around this one, and a drag that starts along an axis this view can't scroll is left to them.
    /// An item's own drag wins over the scroll, since the deeper claim wins, and a pressed item stops being pressed when
    /// the scroll takes the pointer.
    /// </para>
    /// <para>
    /// When a drag is cancelled, because another gesture took the pointer, Esc was pressed, the pointer was lost or the behavior
    /// was disabled, <see cref="Offset"/> returns to its value at the start of the drag.
    /// </para>
    /// </remarks>
    [ProcessNode(Name = "Scrollable", HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public sealed class Scrollable : NodeBase, IBehavior
    {
        private readonly DragGestureRecognizer _drag = new();

        private IChannel<Vector2>? _offset = null;
        private float _wheelStep = 48f;
        private MouseButtons _mouse = MouseButtons.None;
        private bool _touch = true;
        private bool _enabled = true;

        private bool _granted = false;
        private int? _owedRelease = null;
        private Vector2 _offsetAtStart = Vector2.Zero;
        private Vector2 _low = Vector2.Zero;
        private Vector2 _high = Vector2.Zero;
        private SKMatrix _windowToNode = SKMatrix.Identity;
        private Vector2 _pressInWindow = Vector2.Zero;
        private Vector2 _lastInWindow = Vector2.Zero;

        [Fragment]
        public Scrollable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext)
        {
            UpdateButtons();
        }

        /// <summary>
        /// How far the content is scrolled, in this node's units: (0, 0) at the start, positive when scrolled right or down.
        /// The content shows it as the translation by its negation.
        /// </summary>
        public IChannel<Vector2>? Offset => _offset;

        /// <summary>
        /// Connects the channel that holds the offset. Changing it during a drag cancels the drag,
        /// which writes the value at its start to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetOffset(IChannel<Vector2>? offset)
        {
            if (ReferenceEquals(offset, _offset))
                return;

            CancelDrag();
            _offset = offset;
        }

        public float WheelStep => _wheelStep;

        /// <summary>
        /// Sets how far one wheel notch scrolls, in this node's units; 0 turns scrolling with the wheel off.
        /// A step that is negative or isn't a finite number is reported, and the call is ignored.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetWheelStep(float step = 48f)
        {
            if (step == _wheelStep)
                return;

            if (!float.IsFinite(step) || step < 0f)
            {
                this.LogWarning($"The wheel step needs to be a finite number of at least 0, got {step}. It is ignored.");
                return;
            }

            _wheelStep = step;
        }

        public MouseButtons DragButtons => _mouse;

        public bool DragsWithTouch => _touch;

        /// <summary>
        /// Sets which pointers drag the content: the mouse buttons, none by default as on the desktop, and whether touches do.
        /// Applies from the next press.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetDrag(MouseButtons mouse = MouseButtons.None, bool touch = true)
        {
            _mouse = mouse;
            _touch = touch;
            UpdateButtons();
        }

        public bool Enabled => _enabled;

        /// <summary>
        /// Disabling cancels a drag at once, writing the offset at its start, and lets go of the pointer with the next
        /// notification of it the node receives.
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
            SettleOwed(notification);

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

            if (sample.Kind == PointerSampleKind.Wheel)
                return OnWheel(in sample);

            if (sample.Kind == PointerSampleKind.Down && !Drags(in sample))
                return false;

            return OnDragSample(notification, in sample);
        }

        private bool OnDragSample(INotification notification, in PointerSample sample)
        {
            var drag = _drag.Process(in sample);
            switch (drag.Phase)
            {
                case DragPhase.Pressed:
                    if (sample.WindowToNode is not { } windowToNode)
                    {
                        _drag.Cancel();
                        return false;
                    }

                    _windowToNode = windowToNode;
                    _pressInWindow = sample.PositionInWindow;
                    _lastInWindow = sample.PositionInWindow;
                    return false;
                case DragPhase.Started:
                    _lastInWindow = sample.PositionInWindow;
                    if (!TryGetRange(sample.Node, out _low, out _high) || !CanScrollAlong(MovedInNode()))
                    {
                        // Nothing to scroll this way: leave the pointer to a scroll view around this one.
                        _drag.Cancel();
                        return false;
                    }

                    notification.RequestPointerCapture(this, GestureKind.Drag);
                    return false;
                case DragPhase.Moved when _granted:
                    _lastInWindow = sample.PositionInWindow;
                    PublishDrag();
                    return true;
                case DragPhase.Ended when _granted:
                    _lastInWindow = sample.PositionInWindow;
                    _granted = false;
                    PublishDrag();
                    return true;
                default:
                    return false;
            }
        }

        private bool OnGranted(PointerCaptureNotification capture)
        {
            if (_drag.Current.Phase is not (DragPhase.Started or DragPhase.Moved))
            {
                // The drag was cancelled after it asked: the pointer is of no use.
                capture.ReleasePointerCapture(this);
                _drag.Cancel();
                return false;
            }

            _granted = true;
            _offsetAtStart = _offset?.Value ?? Vector2.Zero;
            PublishDrag();
            return true;
        }

        /// <summary>
        /// A notch scrolls by the step: the vertical wheel vertically, the horizontal wheel and Shift + the vertical wheel
        /// horizontally. Processed only when the offset changed, so a wheel at the limit goes on to the ancestors.
        /// During a drag it is processed and changes nothing.
        /// </summary>
        private bool OnWheel(in PointerSample sample)
        {
            if (_wheelStep == 0f)
                return false;

            var notches = sample.WheelDelta;
            var delta =
                notches.X != 0f ? new Vector2(notches.X * _wheelStep, 0f)
                : (sample.Modifiers & Keys.Shift) == Keys.Shift ? new Vector2(-notches.Y * _wheelStep, 0f)
                : new Vector2(0f, -notches.Y * _wheelStep);

            if (delta == Vector2.Zero)
                return false;

            if (_granted)
                return true;

            if (!TryGetRange(sample.Node, out var low, out var high))
                return false;

            var current = _offset?.Value ?? Vector2.Zero;
            var next = Clamp(current + delta, current, low, high);
            if (next == current)
                return false;

            _offset?.EnsureValue(next);
            return true;
        }

        /// <summary>
        /// Moves the content with the pointer: the offset at the start minus the pointer's way since the press, in this node's space.
        /// </summary>
        private void PublishDrag() =>
            _offset?.EnsureValue(Clamp(_offsetAtStart - MovedInNode(), _offsetAtStart, _low, _high));

        private Vector2 MovedInNode() =>
            GestureMath.Map(in _windowToNode, _lastInWindow) - GestureMath.Map(in _windowToNode, _pressInWindow);

        /// <summary>
        /// Whether the content scrolls along the dominant axis of <paramref name="moved"/>, given the current offset.
        /// </summary>
        private bool CanScrollAlong(Vector2 moved)
        {
            var current = _offset?.Value ?? Vector2.Zero;
            var low = Vector2.Min(_low, current);
            var high = Vector2.Max(_high, current);
            return MathF.Abs(moved.X) >= MathF.Abs(moved.Y) ? low.X < high.X : low.Y < high.Y;
        }

        /// <summary>
        /// Clamps <paramref name="offset"/> to the range, which widens to include <paramref name="from"/>,
        /// so an offset outside it doesn't jump.
        /// </summary>
        private static Vector2 Clamp(Vector2 offset, Vector2 from, Vector2 low, Vector2 high) =>
            Vector2.Clamp(offset, Vector2.Min(low, from), Vector2.Max(high, from));

        /// <summary>
        /// The offsets that keep the first child's box, the content, covering the clip area, or the whole box without one.
        /// Each axis includes 0, so content that fits along it gets [0, 0].
        /// </summary>
        private static bool TryGetRange(IInteractive node, out Vector2 low, out Vector2 high)
        {
            low = high = Vector2.Zero;
            var children = node.Children;
            if (children.Count == 0 || node is not IInteractionNode viewportNode || children[0] is not ILayoutable first)
                return false;

            var content = first.Layout;
            var viewport = viewportNode.ClipArea ?? new RectangleF(0f, 0f, node.Layout.Width, node.Layout.Height);
            low = new Vector2(MathF.Min(0f, content.Left - viewport.Left), MathF.Min(0f, content.Top - viewport.Top));
            high = new Vector2(MathF.Max(0f, content.Right - viewport.Right), MathF.Max(0f, content.Bottom - viewport.Bottom));
            return true;
        }

        /// <summary>
        /// A touch counts as the left button, so it is told apart from the mouse here, at the press.
        /// </summary>
        private bool Drags(in PointerSample sample) =>
            sample.PointerId == PointerIds.Mouse ? (_mouse & sample.Button) != 0 : _touch;

        private void UpdateButtons() => _drag.SetButtons(_touch ? _mouse | MouseButtons.Left : _mouse);

        /// <summary>
        /// A drag that published resets, unless this behavior is a member of the claim. A pending one resets unless a weaker
        /// gesture holds the pointer.
        /// </summary>
        private void OnCancelled(PointerCancelNotification cancel)
        {
            if (cancel.IsMember(this))
                return;

            var drag = _drag.Current;
            if (drag.Phase == DragPhase.Idle || drag.PointerId != cancel.PointerId)
                return;

            if (_granted)
            {
                // The claim was taken away or ended, so there is nothing to let go.
                Reset(owesRelease: false);
                return;
            }

            if (!cancel.SparesPending(GestureKind.Drag))
                _drag.Cancel();
        }

        /// <summary>
        /// Cancels the drag from a setter, which has no notification to let go of the pointer with.
        /// The release is owed until the next notification of it.
        /// </summary>
        private void CancelDrag() => Reset(owesRelease: true);

        /// <summary>
        /// Returns to idle. A drag that published writes the offset it started from.
        /// </summary>
        private void Reset(bool owesRelease)
        {
            if (_granted)
            {
                if (owesRelease)
                    _owedRelease = _drag.Current.PointerId;

                _offset?.EnsureValue(_offsetAtStart);
            }

            _granted = false;
            _drag.Cancel();
        }

        private void SettleOwed(INotification notification)
        {
            if (_owedRelease is not { } owed || !PointerIds.TryGet(notification, out var id) || id != owed)
                return;

            _owedRelease = null;
            notification.ReleasePointerCapture(this);
        }
    }
}
