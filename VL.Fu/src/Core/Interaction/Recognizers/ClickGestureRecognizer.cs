using Stride.Core.Mathematics;
using VL.Lib.IO;

namespace VL.Fu.Core.Interaction.Recognizers
{
    /// <summary>
    /// What a sample did to a click.
    /// </summary>
    internal enum ClickPhase
    {
        /// <summary>
        /// Not pressed. Only <see cref="ClickGestureRecognizer.Current"/> reports it.
        /// </summary>
        Idle,

        /// <summary>
        /// The sample changed nothing the click reports: another pointer or node, another button, a move within the threshold.
        /// </summary>
        Unchanged,

        /// <summary>
        /// The pointer went down with one of the click's buttons.
        /// </summary>
        Pressed,

        /// <summary>
        /// The pointer moved past the threshold. It is still held, but this press can't click any more.
        /// </summary>
        Slipped,

        /// <summary>
        /// The pointer went up within the threshold: a click.
        /// </summary>
        Clicked,

        /// <summary>
        /// The pointer went up after slipping: no click.
        /// </summary>
        Released,
    }

    /// <summary>
    /// The state of a click: its phase, its pointer, and where it was pressed in window pixels.
    /// </summary>
    internal readonly record struct ClickGestureState(ClickPhase Phase, int PointerId, Vector2 PressInWindow);

    /// <summary>
    /// Recognizes a click: a press and a release of one pointer that stays within a threshold of where it was pressed.
    /// </summary>
    /// <remarks>
    /// The buttons and the threshold are frozen at press, so changing them affects the next press.
    /// Once the pointer slips past the threshold, the press can't click, even when the pointer comes back.
    /// </remarks>
    internal sealed class ClickGestureRecognizer
    {
        private MouseButtons _buttons = MouseButtons.Left;
        private float _thresholdDip = 6f;

        private ClickPhase _phase = ClickPhase.Idle;
        private int _pointerId = 0;
        private IInteractive? _node = null;
        private MouseButtons _buttonAtPress = MouseButtons.None;
        private float _thresholdDipAtPress = 0f;
        private float _scalingAtPress = 1f;
        private Vector2 _pressInWindow = Vector2.Zero;

        /// <summary>
        /// The buttons that start a click; a touch counts as <see cref="MouseButtons.Left"/>.
        /// </summary>
        public MouseButtons Buttons => _buttons;

        /// <summary>
        /// How far, in DIP, the pointer may move from where it was pressed and still click.
        /// </summary>
        public float ThresholdDip => _thresholdDip;

        /// <summary>
        /// The state after the last sample the click used: <see cref="ClickPhase.Idle"/>, <see cref="ClickPhase.Pressed"/>
        /// or <see cref="ClickPhase.Slipped"/>.
        /// </summary>
        public ClickGestureState Current => new(_phase, _pointerId, _pressInWindow);

        /// <summary>
        /// Sets the buttons that start a click. Applies from the next press.
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
        /// Feeds one sample and returns what it did to the click.
        /// </summary>
        public ClickGestureState Process(in PointerSample sample) =>
            _phase switch
            {
                ClickPhase.Idle => OnIdle(in sample),
                ClickPhase.Pressed or ClickPhase.Slipped => OnHeld(in sample),
                _ => Unchanged(),
            };

        /// <summary>
        /// Forgets the press and returns to the state of a new recognizer. Settings are kept.
        /// </summary>
        public void Cancel()
        {
            _phase = ClickPhase.Idle;
            _pointerId = 0;
            _node = null;
            _buttonAtPress = MouseButtons.None;
            _thresholdDipAtPress = 0f;
            _scalingAtPress = 1f;
            _pressInWindow = Vector2.Zero;
        }

        private ClickGestureState OnIdle(in PointerSample sample)
        {
            if (sample.Kind != PointerSampleKind.Down || (sample.Button & _buttons) == 0)
                return Unchanged();

            _phase = ClickPhase.Pressed;
            _pointerId = sample.PointerId;
            _node = sample.Node;
            _buttonAtPress = sample.Button;
            _thresholdDipAtPress = _thresholdDip;
            _scalingAtPress = PointerSample.DipScaling(sample.Scaling);
            _pressInWindow = sample.PositionInWindow;
            return Current;
        }

        private ClickGestureState OnHeld(in PointerSample sample)
        {
            if (sample.PointerId != _pointerId || !ReferenceEquals(sample.Node, _node))
                return Unchanged();

            switch (sample.Kind)
            {
                case PointerSampleKind.Move:
                    if (_phase == ClickPhase.Slipped || !IsPastThreshold(in sample))
                        return Unchanged();
                    _phase = ClickPhase.Slipped;
                    return Current;

                case PointerSampleKind.Up when sample.Button == _buttonAtPress:
                    var clicked = _phase == ClickPhase.Pressed && !IsPastThreshold(in sample);
                    var released = Current with { Phase = clicked ? ClickPhase.Clicked : ClickPhase.Released };
                    Cancel();
                    return released;

                default:
                    return Unchanged();
            }
        }

        private bool IsPastThreshold(in PointerSample sample)
        {
            var distanceDip = GestureMath.DistanceDip(_pressInWindow, sample.PositionInWindow, _scalingAtPress);
            return distanceDip >= _thresholdDipAtPress;
        }

        private ClickGestureState Unchanged() => Current with { Phase = ClickPhase.Unchanged };
    }
}
