using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Lib.IO;

namespace VL.Fu.Core.Interaction.Recognizers
{
    /// <summary>
    /// What a sample did to a drag.
    /// </summary>
    internal enum DragPhase
    {
        /// <summary>
        /// Not pressed. Only <see cref="DragGestureRecognizer.Current"/> reports it.
        /// </summary>
        Idle,

        /// <summary>
        /// The sample changed nothing the drag reports: another pointer or node, another button, a move within the threshold.
        /// </summary>
        Unchanged,

        /// <summary>
        /// The pointer went down with one of the drag's buttons. The drag is pending until the pointer passes the threshold.
        /// </summary>
        Pressed,

        /// <summary>
        /// The pointer passed the threshold: the drag begins. A behavior requests its claim here.
        /// </summary>
        Started,

        /// <summary>
        /// The pointer moved during the drag.
        /// </summary>
        Moved,

        /// <summary>
        /// The pointer went up during the drag.
        /// </summary>
        Ended,

        /// <summary>
        /// The pointer went up before passing the threshold: no drag.
        /// </summary>
        Released,
    }

    /// <summary>
    /// The state of a drag, in the space of the node's parent as it was at press.
    /// </summary>
    /// <param name="Phase">What the sample did.</param>
    /// <param name="PointerId">The pointer that drags.</param>
    /// <param name="StartInParent">Where the pointer was pressed.</param>
    /// <param name="PositionInParent">Where the pointer is now.</param>
    internal readonly record struct DragGestureState(DragPhase Phase, int PointerId, Vector2 StartInParent, Vector2 PositionInParent)
    {
        /// <summary>
        /// How far the pointer moved since the press. Measured from the press, not from where the threshold was passed,
        /// so a node moved by it stays under the pointer.
        /// </summary>
        public Vector2 OffsetInParent => PositionInParent - StartInParent;
    }

    /// <summary>
    /// Recognizes a drag: one pointer pressed with one of the drag's buttons that moves past a threshold.
    /// </summary>
    /// <remarks>
    /// The buttons, the threshold and the mapping from window pixels into the parent's space are frozen at press,
    /// so the offset stays correct while the dragged node moves, and changed settings affect the next press.
    /// A press while the parent's space is scaled to zero doesn't start a drag.
    /// </remarks>
    internal sealed class DragGestureRecognizer
    {
        private MouseButtons _buttons = MouseButtons.Left;
        private float _thresholdDip = 4f;

        private DragPhase _phase = DragPhase.Idle;
        private int _pointerId = 0;
        private IInteractive? _node = null;
        private MouseButtons _buttonAtPress = MouseButtons.None;
        private float _thresholdDipAtPress = 0f;
        private float _scalingAtPress = 1f;
        private SKMatrix _windowToParentAtPress = SKMatrix.Identity;
        private Vector2 _pressInWindow = Vector2.Zero;
        private Vector2 _startInParent = Vector2.Zero;
        private Vector2 _positionInParent = Vector2.Zero;

        /// <summary>
        /// The buttons that start a drag; a touch counts as <see cref="MouseButtons.Left"/>.
        /// </summary>
        public MouseButtons Buttons => _buttons;

        /// <summary>
        /// How far, in DIP, the pointer has to move from where it was pressed before the drag starts.
        /// </summary>
        public float ThresholdDip => _thresholdDip;

        /// <summary>
        /// The state after the last sample the drag used: <see cref="DragPhase.Idle"/>, <see cref="DragPhase.Pressed"/>,
        /// <see cref="DragPhase.Started"/> or <see cref="DragPhase.Moved"/>.
        /// </summary>
        public DragGestureState Current => new(_phase, _pointerId, _startInParent, _positionInParent);

        /// <summary>
        /// Sets the buttons that start a drag. Applies from the next press.
        /// </summary>
        public void SetButtons(MouseButtons buttons)
        {
            if (buttons == _buttons)
                return;
            _buttons = buttons;
        }

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
        /// Feeds one sample and returns what it did to the drag.
        /// </summary>
        public DragGestureState Process(in PointerSample sample) =>
            _phase switch
            {
                DragPhase.Idle => OnIdle(in sample),
                DragPhase.Pressed => OnPressed(in sample),
                DragPhase.Started or DragPhase.Moved => OnDragging(in sample),
                _ => Unchanged(),
            };

        /// <summary>
        /// Forgets the press and returns to the state of a new recognizer. Settings are kept.
        /// </summary>
        public void Cancel()
        {
            _phase = DragPhase.Idle;
            _pointerId = 0;
            _node = null;
            _buttonAtPress = MouseButtons.None;
            _thresholdDipAtPress = 0f;
            _scalingAtPress = 1f;
            _windowToParentAtPress = SKMatrix.Identity;
            _pressInWindow = Vector2.Zero;
            _startInParent = Vector2.Zero;
            _positionInParent = Vector2.Zero;
        }

        private DragGestureState OnIdle(in PointerSample sample)
        {
            if (sample.Kind != PointerSampleKind.Down || (sample.Button & _buttons) == 0)
                return Unchanged();
            if (sample.WindowToParent is not { } windowToParent)
                return Unchanged();

            _phase = DragPhase.Pressed;
            _pointerId = sample.PointerId;
            _node = sample.Node;
            _buttonAtPress = sample.Button;
            _thresholdDipAtPress = _thresholdDip;
            _scalingAtPress = PointerSample.DipScaling(sample.Scaling);
            _windowToParentAtPress = windowToParent;
            _pressInWindow = sample.PositionInWindow;
            _startInParent = GestureMath.Map(in _windowToParentAtPress, _pressInWindow);
            _positionInParent = _startInParent;
            return Current;
        }

        private DragGestureState OnPressed(in PointerSample sample)
        {
            if (!IsOwn(in sample))
                return Unchanged();

            switch (sample.Kind)
            {
                case PointerSampleKind.Move:
                    var distanceDip = GestureMath.DistanceDip(_pressInWindow, sample.PositionInWindow, _scalingAtPress);
                    if (distanceDip < _thresholdDipAtPress)
                        return Unchanged();
                    _phase = DragPhase.Started;
                    _positionInParent = GestureMath.Map(in _windowToParentAtPress, sample.PositionInWindow);
                    return Current;

                case PointerSampleKind.Up when sample.Button == _buttonAtPress:
                    var released = Current with { Phase = DragPhase.Released };
                    Cancel();
                    return released;

                default:
                    return Unchanged();
            }
        }

        private DragGestureState OnDragging(in PointerSample sample)
        {
            if (!IsOwn(in sample))
                return Unchanged();

            switch (sample.Kind)
            {
                case PointerSampleKind.Move:
                    _phase = DragPhase.Moved;
                    _positionInParent = GestureMath.Map(in _windowToParentAtPress, sample.PositionInWindow);
                    return Current;

                case PointerSampleKind.Up when sample.Button == _buttonAtPress:
                    _positionInParent = GestureMath.Map(in _windowToParentAtPress, sample.PositionInWindow);
                    var ended = Current with { Phase = DragPhase.Ended };
                    Cancel();
                    return ended;

                default:
                    return Unchanged();
            }
        }

        private bool IsOwn(in PointerSample sample) => sample.PointerId == _pointerId && ReferenceEquals(sample.Node, _node);

        private DragGestureState Unchanged() => Current with { Phase = DragPhase.Unchanged };
    }
}
