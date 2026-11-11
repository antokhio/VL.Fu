using System;
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Lib.IO;

namespace VL.Fu.Core.Interaction.Recognizers
{
    /// <summary>
    /// What a sample did to a pinch.
    /// </summary>
    internal enum PinchPhase
    {
        /// <summary>
        /// No pointer is tracked. Only <see cref="PinchGestureRecognizer.Current"/> reports it.
        /// </summary>
        Idle,

        /// <summary>
        /// The sample changed nothing the pinch reports: a third pointer, another node, a move within the threshold.
        /// </summary>
        Unchanged,

        /// <summary>
        /// The first pointer went down.
        /// </summary>
        Pressed,

        /// <summary>
        /// The second pointer went down. The totals are measured from here; the pinch is pending until a pointer passes the threshold.
        /// </summary>
        Paired,

        /// <summary>
        /// A pointer passed the threshold: the pinch begins. A behavior requests its claim for both pointers here.
        /// </summary>
        Started,

        /// <summary>
        /// A pointer moved during the pinch.
        /// </summary>
        Moved,

        /// <summary>
        /// One of the two pointers went up during the pinch.
        /// </summary>
        Ended,

        /// <summary>
        /// A tracked pointer went up before the pinch started: no pinch.
        /// </summary>
        Released,
    }

    /// <summary>
    /// The state of a pinch, in the space of the node's parent as it was when the first pointer was pressed.
    /// The totals are measured from the moment both pointers were down.
    /// </summary>
    /// <param name="Phase">What the sample did.</param>
    /// <param name="FirstPointerId">The pointer pressed first.</param>
    /// <param name="SecondPointerId">The pointer pressed second.</param>
    /// <param name="CenterInParent">The point halfway between the two pointers.</param>
    /// <param name="Scale">The distance between the pointers over their distance when paired. 1 when they started at one point.</param>
    /// <param name="Rotation">How far the line between the pointers turned, in radians, counting full turns.</param>
    /// <param name="TranslationInParent">How far the center moved.</param>
    internal readonly record struct PinchGestureState(
        PinchPhase Phase,
        int FirstPointerId,
        int SecondPointerId,
        Vector2 CenterInParent,
        float Scale,
        float Rotation,
        Vector2 TranslationInParent
    );

    /// <summary>
    /// Recognizes a pinch: two pointers pressed on one node, one of which moves past a threshold.
    /// It reports scale, rotation and translation about the center of the two.
    /// </summary>
    /// <remarks>
    /// Both pointers are mapped through the parent space frozen when the first one was pressed, so they share one space.
    /// A third pointer is ignored. When either pointer lifts, the pinch ends, and the other pointer isn't tracked any more:
    /// a new pinch needs two new presses.
    /// </remarks>
    internal sealed class PinchGestureRecognizer
    {
        private const float MinAnchorDistance = 1e-4f;

        private float _thresholdDip = 4f;

        private PinchPhase _phase = PinchPhase.Idle;
        private IInteractive? _node = null;
        private float _thresholdDipAtPress = 0f;
        private float _scalingAtPress = 1f;
        private SKMatrix _windowToParentAtPress = SKMatrix.Identity;

        private int _firstPointerId = 0;
        private MouseButtons _firstButton = MouseButtons.None;
        private Vector2 _firstInWindow = Vector2.Zero;
        private Vector2 _firstAnchorInWindow = Vector2.Zero;

        private int _secondPointerId = 0;
        private MouseButtons _secondButton = MouseButtons.None;
        private Vector2 _secondInWindow = Vector2.Zero;
        private Vector2 _secondAnchorInWindow = Vector2.Zero;

        private Vector2 _centerAnchorInParent = Vector2.Zero;
        private float _distanceAnchor = 0f;
        private float _lastAngle = 0f;

        private Vector2 _centerInParent = Vector2.Zero;
        private float _scale = 1f;
        private float _rotation = 0f;
        private Vector2 _translationInParent = Vector2.Zero;

        /// <summary>
        /// How far, in DIP, one of the pointers has to move from where it was when both were down before the pinch starts.
        /// </summary>
        public float ThresholdDip => _thresholdDip;

        /// <summary>
        /// The state after the last sample the pinch used: <see cref="PinchPhase.Idle"/>, <see cref="PinchPhase.Pressed"/>,
        /// <see cref="PinchPhase.Paired"/>, <see cref="PinchPhase.Started"/> or <see cref="PinchPhase.Moved"/>.
        /// </summary>
        public PinchGestureState Current =>
            new(_phase, _firstPointerId, _secondPointerId, _centerInParent, _scale, _rotation, _translationInParent);

        /// <summary>
        /// Sets the threshold in DIP. Applies from the next press. A negative or non-finite value is ignored.
        /// </summary>
        public void SetThresholdDip(float thresholdDip)
        {
            if (thresholdDip == _thresholdDip || !GestureMath.IsValidThreshold(thresholdDip))
                return;
            _thresholdDip = thresholdDip;
        }

        /// <summary>
        /// Feeds one sample and returns what it did to the pinch.
        /// </summary>
        public PinchGestureState Process(in PointerSample sample) =>
            _phase switch
            {
                PinchPhase.Idle => OnIdle(in sample),
                PinchPhase.Pressed => OnPressed(in sample),
                PinchPhase.Paired => OnPaired(in sample),
                PinchPhase.Started or PinchPhase.Moved => OnPinching(in sample),
                _ => Unchanged(),
            };

        /// <summary>
        /// Forgets both pointers and returns to the state of a new recognizer. Settings are kept.
        /// </summary>
        public void Cancel()
        {
            _phase = PinchPhase.Idle;
            _node = null;
            _thresholdDipAtPress = 0f;
            _scalingAtPress = 1f;
            _windowToParentAtPress = SKMatrix.Identity;

            _firstPointerId = 0;
            _firstButton = MouseButtons.None;
            _firstInWindow = Vector2.Zero;
            _firstAnchorInWindow = Vector2.Zero;

            _secondPointerId = 0;
            _secondButton = MouseButtons.None;
            _secondInWindow = Vector2.Zero;
            _secondAnchorInWindow = Vector2.Zero;

            _centerAnchorInParent = Vector2.Zero;
            _distanceAnchor = 0f;
            _lastAngle = 0f;

            _centerInParent = Vector2.Zero;
            _scale = 1f;
            _rotation = 0f;
            _translationInParent = Vector2.Zero;
        }

        private PinchGestureState OnIdle(in PointerSample sample)
        {
            if (sample.Kind != PointerSampleKind.Down)
                return Unchanged();
            if (sample.WindowToParent is not { } windowToParent)
                return Unchanged();

            _phase = PinchPhase.Pressed;
            _node = sample.Node;
            _thresholdDipAtPress = _thresholdDip;
            _scalingAtPress = PointerSample.DipScaling(sample.Scaling);
            _windowToParentAtPress = windowToParent;
            _firstPointerId = sample.PointerId;
            _firstButton = sample.Button;
            _firstInWindow = sample.PositionInWindow;
            _centerInParent = GestureMath.Map(in _windowToParentAtPress, _firstInWindow);
            return Current;
        }

        private PinchGestureState OnPressed(in PointerSample sample)
        {
            if (!ReferenceEquals(sample.Node, _node))
                return Unchanged();

            if (sample.PointerId == _firstPointerId)
            {
                switch (sample.Kind)
                {
                    case PointerSampleKind.Move:
                        _firstInWindow = sample.PositionInWindow;
                        _centerInParent = GestureMath.Map(in _windowToParentAtPress, _firstInWindow);
                        return Unchanged();
                    case PointerSampleKind.Up when sample.Button == _firstButton:
                        return Release();
                    default:
                        return Unchanged();
                }
            }

            if (sample.Kind != PointerSampleKind.Down)
                return Unchanged();

            _phase = PinchPhase.Paired;
            _secondPointerId = sample.PointerId;
            _secondButton = sample.Button;
            _secondInWindow = sample.PositionInWindow;
            _firstAnchorInWindow = _firstInWindow;
            _secondAnchorInWindow = _secondInWindow;

            var firstInParent = GestureMath.Map(in _windowToParentAtPress, _firstInWindow);
            var secondInParent = GestureMath.Map(in _windowToParentAtPress, _secondInWindow);
            var between = secondInParent - firstInParent;
            _centerAnchorInParent = (firstInParent + secondInParent) / 2f;
            _distanceAnchor = between.Length();
            _lastAngle = MathF.Atan2(between.Y, between.X);

            _centerInParent = _centerAnchorInParent;
            _scale = 1f;
            _rotation = 0f;
            _translationInParent = Vector2.Zero;
            return Current;
        }

        private PinchGestureState OnPaired(in PointerSample sample)
        {
            if (!IsTracked(in sample, out var isFirst))
                return Unchanged();

            switch (sample.Kind)
            {
                case PointerSampleKind.Move:
                    Track(isFirst, sample.PositionInWindow);
                    if (!IsPastThreshold())
                        return Unchanged();
                    _phase = PinchPhase.Started;
                    return Current;

                case PointerSampleKind.Up when sample.Button == (isFirst ? _firstButton : _secondButton):
                    return Release();

                default:
                    return Unchanged();
            }
        }

        private PinchGestureState OnPinching(in PointerSample sample)
        {
            if (!IsTracked(in sample, out var isFirst))
                return Unchanged();

            switch (sample.Kind)
            {
                case PointerSampleKind.Move:
                    Track(isFirst, sample.PositionInWindow);
                    _phase = PinchPhase.Moved;
                    return Current;

                case PointerSampleKind.Up when sample.Button == (isFirst ? _firstButton : _secondButton):
                    Track(isFirst, sample.PositionInWindow);
                    var ended = Current with { Phase = PinchPhase.Ended };
                    Cancel();
                    return ended;

                default:
                    return Unchanged();
            }
        }

        private bool IsTracked(in PointerSample sample, out bool isFirst)
        {
            isFirst = sample.PointerId == _firstPointerId;
            if (!ReferenceEquals(sample.Node, _node))
                return false;
            return isFirst || sample.PointerId == _secondPointerId;
        }

        private void Track(bool isFirst, Vector2 positionInWindow)
        {
            if (isFirst)
                _firstInWindow = positionInWindow;
            else
                _secondInWindow = positionInWindow;

            var firstInParent = GestureMath.Map(in _windowToParentAtPress, _firstInWindow);
            var secondInParent = GestureMath.Map(in _windowToParentAtPress, _secondInWindow);
            var between = secondInParent - firstInParent;
            var angle = MathF.Atan2(between.Y, between.X);

            _centerInParent = (firstInParent + secondInParent) / 2f;
            _scale = _distanceAnchor < MinAnchorDistance ? 1f : between.Length() / _distanceAnchor;
            _rotation += GestureMath.Wrap(angle - _lastAngle);
            _lastAngle = angle;
            _translationInParent = _centerInParent - _centerAnchorInParent;
        }

        private bool IsPastThreshold()
        {
            var firstDistanceDip = GestureMath.DistanceDip(_firstAnchorInWindow, _firstInWindow, _scalingAtPress);
            var secondDistanceDip = GestureMath.DistanceDip(_secondAnchorInWindow, _secondInWindow, _scalingAtPress);
            return firstDistanceDip >= _thresholdDipAtPress || secondDistanceDip >= _thresholdDipAtPress;
        }

        private PinchGestureState Release()
        {
            var released = Current with { Phase = PinchPhase.Released };
            Cancel();
            return released;
        }

        private PinchGestureState Unchanged() => Current with { Phase = PinchPhase.Unchanged };
    }
}
