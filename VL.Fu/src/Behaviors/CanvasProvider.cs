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
    /// Makes the node it is attached to a pannable, zoomable view of its content: the right button drags to pan,
    /// the wheel zooms about the cursor, and two touches zoom about their center and pan with it.
    /// Connect a channel to <see cref="View"/> and apply it to the content node's transformation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The content is the node's first child. Changes are applied about the content's transform origin, so the point under
    /// the cursor or the touches stays under them whatever origin the content has. Give the content a fixed size, or the origin
    /// (−1, −1), so that pivot doesn't move when the content grows.
    /// </para>
    /// <para>
    /// The view only scales and translates, and the scale stays between the limits of <see cref="SetZoom"/>. A view set outside
    /// the limits stays where it is and can only move back towards them. The wheel bubbles from the node under the cursor,
    /// so an inner canvas or scroll view that takes it wins, and this canvas takes it even at a limit, so an outer one doesn't zoom instead.
    /// </para>
    /// <para>
    /// A pan and a pinch don't run together, and the wheel does nothing while either runs. When a pan or a pinch is cancelled,
    /// because another gesture took the pointer, Esc was pressed, the pointer was lost or the behavior was disabled,
    /// <see cref="View"/> returns to its value at the start of the gesture.
    /// </para>
    /// </remarks>
    [ProcessNode(Name = "CanvasProvider", HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public sealed class CanvasProvider : NodeBase, IBehavior
    {
        private readonly DragGestureRecognizer _pan = new();
        private readonly PinchGestureRecognizer _pinch = new();
        private readonly int[] _pointers = new int[2];

        private IChannel<Matrix>? _view = null;
        private float _minZoom = 0.1f;
        private float _maxZoom = 10f;
        private float _wheelStep = 1.2f;
        private bool _enabled = true;

        private bool _panGranted = false;
        private bool _pinchGranted = false;
        private int? _owedRelease = null;
        private int? _owedSecond = null;
        private Matrix _viewAtStart = Matrix.Identity;
        private Vector2 _pivotAtStart = Vector2.Zero;
        private SKMatrix _panWindowToNode = SKMatrix.Identity;
        private Vector2 _panPressInWindow = Vector2.Zero;
        private Vector2 _panLastInWindow = Vector2.Zero;
        private SKMatrix _pinchParentToNode = SKMatrix.Identity;

        [Fragment]
        public CanvasProvider([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext)
        {
            _pan.SetButtons(MouseButtons.Right);
        }

        /// <summary>
        /// The content's transformation: how far it is panned and zoomed, about its transform origin.
        /// </summary>
        public IChannel<Matrix>? View => _view;

        /// <summary>
        /// Connects the channel that holds the view. Changing it during a pan or a pinch cancels the gesture,
        /// which writes the value at its start to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetView(IChannel<Matrix>? view)
        {
            if (ReferenceEquals(view, _view))
                return;

            CancelGesture();
            _view = view;
        }

        /// <summary>
        /// The buttons that pan.
        /// </summary>
        public MouseButtons PanButtons => _pan.Buttons;

        /// <summary>
        /// Sets the mouse buttons that pan; <see cref="MouseButtons.None"/> turns panning with the mouse off.
        /// Touches don't pan alone: two of them pan through the pinch. Applies from the next press.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetPan(MouseButtons buttons = MouseButtons.Right) => _pan.SetButtons(buttons);

        public float MinZoom => _minZoom;

        public float MaxZoom => _maxZoom;

        public float WheelStep => _wheelStep;

        /// <summary>
        /// Sets the smallest and the largest scale, and the factor one wheel notch zooms by; a step of 1 turns zooming with
        /// the wheel off. Limits that aren't positive finite numbers, a minimum above the maximum, or a step that isn't
        /// a positive finite number are reported, and the call is ignored.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetZoom(float min = 0.1f, float max = 10f, float wheelStep = 1.2f)
        {
            if (min == _minZoom && max == _maxZoom && wheelStep == _wheelStep)
                return;

            if (!IsPositive(min) || !IsPositive(max) || min > max || !IsPositive(wheelStep))
            {
                this.LogWarning(
                    $"Zoom needs 0 < min ≤ max and a positive wheel step, all finite, got min {min}, max {max}, wheel step {wheelStep}. It is ignored."
                );
                return;
            }

            _minZoom = min;
            _maxZoom = max;
            _wheelStep = wheelStep;
        }

        public bool Enabled => _enabled;

        /// <summary>
        /// Disabling cancels a pan or a pinch at once, writing the view at its start, and lets go of the pointers with the next
        /// notification of one of them the node receives.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            if (enabled == _enabled)
                return;

            if (!enabled)
                CancelGesture();

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
                    return capture.Claim.Kind == GestureKind.Pinch ? OnPinchGranted(capture) : OnPanGranted(capture);
                case PointerCancelNotification cancel:
                    OnCancelled(cancel);
                    return false;
            }

            if (!PointerInput.TryRead(notification, out var sample))
                return false;

            if (sample.Kind == PointerSampleKind.Wheel)
                return OnWheel(in sample);

            // The mouse pans and touches pinch (D5): a touch is the left button, which belongs to selection and items.
            return sample.PointerId == PointerIds.Mouse
                ? OnPanSample(notification, in sample)
                : OnPinchSample(notification, in sample);
        }

        private bool OnPinchSample(INotification notification, in PointerSample sample)
        {
            var pinch = _pinch.Process(in sample);
            switch (pinch.Phase)
            {
                case PinchPhase.Pressed:
                    if (!TryFreezeParentToNode(in sample))
                        _pinch.Cancel();
                    return false;
                case PinchPhase.Paired:
                case PinchPhase.Started:
                    if (_panGranted)
                    {
                        // The mouse pans: the touches don't zoom until it lets go (D4).
                        _pinch.Cancel();
                        return false;
                    }

                    if (pinch.Phase == PinchPhase.Started)
                    {
                        _pointers[0] = pinch.FirstPointerId;
                        _pointers[1] = pinch.SecondPointerId;
                        notification.RequestPointerCapture(this, GestureKind.Pinch, _pointers);
                    }
                    return false;
                case PinchPhase.Moved when _pinchGranted:
                    PublishPinch(in pinch);
                    return true;
                case PinchPhase.Ended when _pinchGranted:
                    _pinchGranted = false;
                    PublishPinch(in pinch);
                    // The other touch is still down, but this pinch is over: let it go back to the nodes it was pressed on.
                    notification.ReleasePointerCapture(this);
                    return true;
                default:
                    return false;
            }
        }
        private bool OnPanSample(INotification notification, in PointerSample sample)
        {
            var pan = _pan.Process(in sample);
            switch (pan.Phase)
            {
                case DragPhase.Pressed:
                    if (sample.WindowToNode is not { } windowToNode)
                    {
                        _pan.Cancel();
                        return false;
                    }

                    _panWindowToNode = windowToNode;
                    _panPressInWindow = sample.PositionInWindow;
                    _panLastInWindow = sample.PositionInWindow;
                    return false;
                case DragPhase.Started:
                    _panLastInWindow = sample.PositionInWindow;
                    if (_pinchGranted)
                    {
                        _pan.Cancel();
                        return false;
                    }

                    notification.RequestPointerCapture(this, GestureKind.Drag);
                    return false;
                case DragPhase.Moved when _panGranted:
                    _panLastInWindow = sample.PositionInWindow;
                    PublishPan();
                    return true;
                case DragPhase.Ended when _panGranted:
                    _panLastInWindow = sample.PositionInWindow;
                    _panGranted = false;
                    PublishPan();
                    return true;
                default:
                    return false;
            }
        }

        private bool OnPanGranted(PointerCaptureNotification capture)
        {
            if (_pan.Current.Phase is not (DragPhase.Started or DragPhase.Moved) || _pinchGranted || !TryFreeze(capture))
            {
                // The pan was cancelled after it asked, or a pinch runs: the pointer is of no use.
                capture.ReleasePointerCapture(this);
                _pan.Cancel();
                return false;
            }

            _panGranted = true;
            PublishPan();
            return true;
        }

        private bool OnPinchGranted(PointerCaptureNotification capture)
        {
            var current = _pinch.Current;
            if (current.Phase is not (PinchPhase.Started or PinchPhase.Moved) || _panGranted || !TryFreeze(capture))
            {
                // The pinch was cancelled after it asked, or a pan runs: the touches are of no use.
                capture.ReleasePointerCapture(this);
                _pinch.Cancel();
                return false;
            }

            _pinchGranted = true;
            PublishPinch(in current);
            return true;
        }

        /// <summary>
        /// A vertical notch zooms about the cursor. Processed even at a limit or during a pan or a pinch, so an outer canvas
        /// doesn't zoom instead.
        /// </summary>
        private bool OnWheel(in PointerSample sample)
        {
            var notches = sample.WheelDelta.Y;
            if (notches == 0f || _wheelStep == 1f)
                return false;

            if (_panGranted || _pinchGranted)
                return true;

            if (!sample.TryGetPositionInNode(out var cursorInNode))
                return false;

            var view = _view?.Value ?? Matrix.Identity;
            var factor = ClampFactor(GestureMath.ScaleOf(in view), MathF.Pow(_wheelStep, notches));
            var delta = SKMatrix
                .CreateTranslation(cursorInNode.X, cursorInNode.Y)
                .PreConcat(SKMatrix.CreateScale(factor, factor))
                .PreConcat(SKMatrix.CreateTranslation(-cursorInNode.X, -cursorInNode.Y));

            _view?.EnsureValue(GestureMath.ComposeAboutPivot(in view, PivotOfContent(sample.Node), in delta));
            return true;
        }

        /// <summary>
        /// Translates by the pointer's offset since the press, in the node's space. A translation needs no pivot compensation.
        /// </summary>
        private void PublishPan()
        {
            var offsetInNode =
                GestureMath.Map(in _panWindowToNode, _panLastInWindow) - GestureMath.Map(in _panWindowToNode, _panPressInWindow);

            _view?.EnsureValue(_viewAtStart * Matrix.Translation(offsetInNode.X, offsetInNode.Y, 0f));
        }

        /// <summary>
        /// Scales about the center at pairing and moves it to the current center, both in the node's space:
        /// <c>T(center) · S(k) · T(−centerAtPair)</c>. The rotation is ignored.
        /// </summary>
        private void PublishPinch(in PinchGestureState pinch)
        {
            var centerInNode = GestureMath.Map(in _pinchParentToNode, pinch.CenterInParent);
            var atPairInNode = GestureMath.Map(in _pinchParentToNode, pinch.CenterInParent - pinch.TranslationInParent);
            var factor = ClampFactor(GestureMath.ScaleOf(in _viewAtStart), pinch.Scale);
            var delta = SKMatrix
                .CreateTranslation(centerInNode.X, centerInNode.Y)
                .PreConcat(SKMatrix.CreateScale(factor, factor))
                .PreConcat(SKMatrix.CreateTranslation(-atPairInNode.X, -atPairInNode.Y));

            _view?.EnsureValue(GestureMath.ComposeAboutPivot(in _viewAtStart, _pivotAtStart, in delta));
        }

        /// <summary>
        /// The factor that takes <paramref name="scale"/> by <paramref name="factor"/> but not past the limits, which widen to
        /// include <paramref name="scale"/>, so a view outside them doesn't jump. 1 for a view that doesn't scale.
        /// </summary>
        private float ClampFactor(float scale, float factor)
        {
            if (!IsPositive(scale) || !float.IsFinite(factor))
                return 1f;

            var lowest = MathF.Min(_minZoom, scale);
            var highest = MathF.Max(_maxZoom, scale);
            return Math.Clamp(scale * factor, lowest, highest) / scale;
        }

        /// <summary>
        /// A gesture that published resets, unless this behavior is a member of the claim. A pending one resets unless a weaker
        /// gesture holds the pointer, so a pinch waiting for its second touch survives a child's drag.
        /// </summary>
        private void OnCancelled(PointerCancelNotification cancel)
        {
            if (cancel.IsMember(this))
                return;

            var pan = _pan.Current;
            var panTracks = pan.Phase != DragPhase.Idle && pan.PointerId == cancel.PointerId;
            var pinchTracks = PinchTracks(cancel.PointerId);
            if ((panTracks && _panGranted) || (pinchTracks && _pinchGranted))
            {
                // The claim was taken away or ended, so there is nothing to let go.
                Reset(owesRelease: false);
                return;
            }

            if (panTracks && !cancel.SparesPending(GestureKind.Drag))
                _pan.Cancel();

            if (pinchTracks && !cancel.SparesPending(GestureKind.Pinch))
                _pinch.Cancel();
        }

        private bool PinchTracks(int pointerId)
        {
            var current = _pinch.Current;
            return current.Phase switch
            {
                PinchPhase.Idle => false,
                PinchPhase.Pressed => current.FirstPointerId == pointerId,
                _ => current.FirstPointerId == pointerId || current.SecondPointerId == pointerId,
            };
        }

        /// <summary>
        /// Cancels the gesture from a setter, which has no notification to let go of the pointers with.
        /// The release is owed until the next notification of one of them.
        /// </summary>
        private void CancelGesture() => Reset(owesRelease: true);

        /// <summary>
        /// Returns to idle. A gesture that published writes the view it started from.
        /// </summary>
        private void Reset(bool owesRelease)
        {
            if (_panGranted || _pinchGranted)
            {
                if (owesRelease)
                {
                    var pinch = _pinch.Current;
                    _owedRelease = _pinchGranted ? pinch.FirstPointerId : _pan.Current.PointerId;
                    _owedSecond = _pinchGranted ? pinch.SecondPointerId : null;
                }

                _view?.EnsureValue(_viewAtStart);
            }

            _panGranted = false;
            _pinchGranted = false;
            _pan.Cancel();
            _pinch.Cancel();
        }

        // A pinch's claim holds both touches, so releasing through either of them lets go of both.
        private void SettleOwed(INotification notification)
        {
            if (_owedRelease is not { } first || !PointerIds.TryGet(notification, out var id))
                return;

            if (id != first && id != _owedSecond)
                return;

            _owedRelease = null;
            _owedSecond = null;
            notification.ReleasePointerCapture(this);
        }

        /// <summary>
        /// Remembers the view and the content's pivot the granted gesture starts from.
        /// </summary>
        private bool TryFreeze(PointerCaptureNotification capture)
        {
            if (capture.Sender is not FuNotificationSender { Node: var node })
                return false;

            _viewAtStart = _view?.Value ?? Matrix.Identity;
            _pivotAtStart = PivotOfContent(node);
            return true;
        }

        /// <summary>
        /// The pinch measures in the parent's space frozen at the first press; the view works in the node's own space.
        /// </summary>
        private bool TryFreezeParentToNode(in PointerSample sample)
        {
            if (
                sample.WindowToNode is not { } windowToNode
                || sample.WindowToParent is not { } windowToParent
                || !windowToParent.TryInvert(out var parentToWindow)
            )
                return false;

            _pinchParentToNode = windowToNode.PreConcat(parentToWindow);
            return true;
        }

        /// <summary>
        /// The pivot of the first child, the content, in this node's space; the origin without one.
        /// </summary>
        private static Vector2 PivotOfContent(IInteractive node)
        {
            var children = node.Children;
            return children.Count > 0 && children[0] is IInteractionNode content ? content.TransformPivotInParent : Vector2.Zero;
        }

        private static bool IsPositive(float value) => float.IsFinite(value) && value > 0f;
    }
}
