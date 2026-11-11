using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Behaviors
{
    /// <summary>
    /// Makes the node it is attached to pinchable: two touches pressed on it, one of which moves further than <see cref="Threshold"/>.
    /// It reports how the two turned, scaled and moved about their center. Attach it to a Fu node's behaviors and connect
    /// channels to the outputs you need.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a touch passes the threshold, the pinch asks for both touches. A pinch is stronger than a drag, so it takes the touches
    /// over from a drag inside the node, and that drag is cancelled. Only once the touches are granted, the pinch publishes.
    /// </para>
    /// <para>
    /// Positions are in the space of the node's parent as it was when the first touch was pressed. The totals are measured
    /// from the moment both touches were down, and keep their last values when a touch lifts. When the pinch is cancelled instead,
    /// they return to their idle values: scale 1, no rotation, no translation. Lifting one touch ends the pinch;
    /// the other one doesn't pinch again until a new touch joins it.
    /// </para>
    /// </remarks>
    [ProcessNode(
        Name = "Pinchable",
        HasStateOutput = true,
        FragmentSelection = FragmentSelection.Explicit
    )]
    public sealed class Pinchable : NodeBase, IBehavior
    {
        private readonly PinchGestureRecognizer _pinch = new();
        private readonly int[] _pointers = new int[2];

        private IChannel<float>? _scale = null;
        private IChannel<float>? _rotation = null;
        private IChannel<Vector2>? _translation = null;
        private IChannel<Vector2>? _center = null;
        private IChannel<bool>? _isPinching = null;
        private bool _enabled = true;

        private bool _granted = false;
        private int? _owedRelease = null;

        [Fragment]
        public Pinchable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// The distance between the touches over their distance when both went down.
        /// </summary>
        public IChannel<float>? Scale => _scale;

        /// <summary>
        /// Connects the channel that receives the scale. Changing it during a pinch cancels the pinch,
        /// which writes 1 to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetScale(IChannel<float>? scale)
        {
            if (ReferenceEquals(scale, _scale))
                return;

            CancelPinch();
            _scale = scale;
        }

        /// <summary>
        /// How far the line between the touches turned, in radians, counting full turns.
        /// </summary>
        public IChannel<float>? Rotation => _rotation;

        /// <summary>
        /// Connects the channel that receives the rotation. Changing it during a pinch cancels the pinch,
        /// which writes 0 to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetRotation(IChannel<float>? rotation)
        {
            if (ReferenceEquals(rotation, _rotation))
                return;

            CancelPinch();
            _rotation = rotation;
        }

        /// <summary>
        /// How far the center between the touches moved.
        /// </summary>
        public IChannel<Vector2>? Translation => _translation;

        /// <summary>
        /// Connects the channel that receives the translation. Changing it during a pinch cancels the pinch,
        /// which writes zero to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetTranslation(IChannel<Vector2>? translation)
        {
            if (ReferenceEquals(translation, _translation))
                return;

            CancelPinch();
            _translation = translation;
        }

        /// <summary>
        /// The point halfway between the touches: the point the pinch scales and turns about.
        /// It keeps its last value when the pinch ends or is cancelled.
        /// </summary>
        public IChannel<Vector2>? Center => _center;

        /// <summary>
        /// Connects the channel that receives the center. Changing it during a pinch cancels the pinch.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetCenter(IChannel<Vector2>? center)
        {
            if (ReferenceEquals(center, _center))
                return;

            CancelPinch();
            _center = center;
        }

        /// <summary>
        /// True from the start of the pinch until it ends or is cancelled.
        /// </summary>
        public IChannel<bool>? IsPinching => _isPinching;

        /// <summary>
        /// Connects the channel that receives whether the node is pinched. Changing it during a pinch cancels the pinch,
        /// which writes false to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetIsPinching(IChannel<bool>? isPinching)
        {
            if (ReferenceEquals(isPinching, _isPinching))
                return;

            CancelPinch();
            _isPinching = isPinching;
        }

        /// <summary>
        /// How far, in DIP, one of the touches has to move from where it was when both were down before the pinch starts.
        /// </summary>
        public float Threshold => _pinch.ThresholdDip;

        /// <summary>
        /// Sets the threshold in DIP. Applies from the next press. A negative or non-finite value is reported and ignored.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetThreshold(float threshold = 4f)
        {
            if (threshold == _pinch.ThresholdDip)
                return;

            if (!GestureMath.IsValidThreshold(threshold))
            {
                this.LogWarning(
                    $"Threshold must be a finite number of at least 0, got {threshold}. It is ignored."
                );
                return;
            }

            _pinch.SetThresholdDip(threshold);
        }

        public bool Enabled => _enabled;

        /// <summary>
        /// Disabling cancels a pinch at once, writing the idle values. The touches are let go with the next notification
        /// of one of them the node receives.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            if (enabled == _enabled)
                return;

            if (!enabled)
                CancelPinch();

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

            var pinch = _pinch.Process(in sample);
            switch (pinch.Phase)
            {
                case PinchPhase.Started:
                    _pointers[0] = pinch.FirstPointerId;
                    _pointers[1] = pinch.SecondPointerId;
                    notification.RequestPointerCapture(this, GestureKind.Pinch, _pointers);
                    return false;
                case PinchPhase.Moved when _granted:
                    Publish(in pinch);
                    return true;
                case PinchPhase.Ended when _granted:
                    _granted = false;
                    Publish(in pinch);
                    _isPinching?.EnsureValue(false);
                    // The other touch is still down, but this pinch is over: let it go back to the nodes it was pressed on.
                    notification.ReleasePointerCapture(this);
                    return true;
                default:
                    return false;
            }
        }

        private bool OnGranted(PointerCaptureNotification capture)
        {
            var current = _pinch.Current;
            if (current.Phase is not (PinchPhase.Started or PinchPhase.Moved))
            {
                // The pinch was cancelled after it asked: the touches are of no use any more.
                capture.ReleasePointerCapture(this);
                return false;
            }

            _granted = true;
            Publish(in current);
            _isPinching?.EnsureValue(true);
            return true;
        }

        private void Publish(in PinchGestureState pinch)
        {
            _scale?.EnsureValue(pinch.Scale);
            _rotation?.EnsureValue(pinch.Rotation);
            _translation?.EnsureValue(pinch.TranslationInParent);
            _center?.EnsureValue(pinch.CenterInParent);
        }

        /// <summary>
        /// Resets unless this pinch is a member of the claim, or still pending while a weaker gesture holds the touch.
        /// </summary>
        private void OnCancelled(PointerCancelNotification cancel)
        {
            if (!Tracks(cancel.PointerId) || cancel.IsMember(this))
                return;

            if (!_granted && cancel.SparesPending(GestureKind.Pinch))
                return;

            // The claim was taken away or ended, so there is nothing to let go.
            Reset(owesRelease: false);
        }

        private bool Tracks(int pointerId)
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
        /// Cancels the pinch from a setter, which has no notification to let go of the touches with.
        /// The release is owed until the next notification of one of them.
        /// </summary>
        private void CancelPinch() => Reset(owesRelease: true);

        /// <summary>
        /// Returns to idle. A pinch that published writes its idle values (B7); the center keeps its last one.
        /// </summary>
        private void Reset(bool owesRelease)
        {
            if (_granted)
            {
                if (owesRelease)
                    _owedRelease = _pinch.Current.FirstPointerId;

                _scale?.EnsureValue(1f);
                _rotation?.EnsureValue(0f);
                _translation?.EnsureValue(Vector2.Zero);
                _isPinching?.EnsureValue(false);
            }

            _granted = false;
            _pinch.Cancel();
        }

        // A claim holds both touches, so releasing through either of them lets go of both.
        private void ReleaseOwedClaim(INotification notification)
        {
            if (_owedRelease is not { } first || !PointerIds.TryGet(notification, out var id))
                return;

            if (id != first && id != _pointers[1])
                return;

            _owedRelease = null;
            notification.ReleasePointerCapture(this);
        }
    }
}
