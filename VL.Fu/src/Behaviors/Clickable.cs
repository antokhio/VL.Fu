using System.Reactive;
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
    /// Makes the node it is attached to clickable: a press and a release of one pointer that stays within <see cref="Threshold"/>
    /// of where it was pressed. Attach it to a Fu node's behaviors and connect channels to <see cref="OnClick"/> and <see cref="IsPressed"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="IsPressed"/> turns true with the press. It stays true while the pointer is held, also after it moved too far to click,
    /// and while another behavior of the same node, such as a drag, took the pointer over. It turns false at once when a gesture
    /// of another node takes the pointer. A click is reported as processed, so an ancestor's click doesn't fire too.
    /// A click never claims the pointer.
    /// </remarks>
    [ProcessNode(
        Name = "Clickable",
        HasStateOutput = true,
        FragmentSelection = FragmentSelection.Explicit
    )]
    public sealed class Clickable : NodeBase, IBehavior
    {
        private readonly ClickGestureRecognizer _click = new();

        private IChannel<Unit>? _onClick = null;
        private IChannel<bool>? _isPressed = null;
        private bool _enabled = true;

        private bool _pressed = false;
        private bool _holdUntilUp = false;
        private int _pointerId = 0;
        private MouseButtons _button = MouseButtons.None;
        private IInteractive? _node = null;

        [Fragment]
        public Clickable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// Fires once per click, on the release.
        /// </summary>
        public IChannel<Unit>? OnClick => _onClick;

        /// <summary>
        /// Connects the channel that fires on a click. Changing it during a press cancels the press.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetOnClick(IChannel<Unit>? onClick)
        {
            if (ReferenceEquals(onClick, _onClick))
                return;

            CancelPress();
            _onClick = onClick;
        }

        /// <summary>
        /// True from the press until the release.
        /// </summary>
        public IChannel<bool>? IsPressed => _isPressed;

        /// <summary>
        /// Connects the channel that receives whether the node is pressed. Changing it during a press cancels the press,
        /// which writes false to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetIsPressed(IChannel<bool>? isPressed)
        {
            if (ReferenceEquals(isPressed, _isPressed))
                return;

            CancelPress();
            _isPressed = isPressed;
        }

        /// <summary>
        /// The buttons that press; a touch counts as <see cref="MouseButtons.Left"/>.
        /// </summary>
        public MouseButtons Buttons => _click.Buttons;

        /// <summary>
        /// Sets the buttons that press. Applies from the next press.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetButtons(MouseButtons buttons = MouseButtons.Left)
        {
            if (buttons == _click.Buttons)
                return;

            if (buttons == MouseButtons.None)
                this.LogWarning("Buttons is None, so nothing presses this node.");

            _click.SetButtons(buttons);
        }

        /// <summary>
        /// How far, in DIP, the pointer may move from where it was pressed and still click.
        /// </summary>
        public float Threshold => _click.ThresholdDip;

        /// <summary>
        /// Sets the threshold in DIP. Applies from the next press. A negative or non-finite value is reported and ignored.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetThreshold(float threshold = 6f)
        {
            if (threshold == _click.ThresholdDip)
                return;

            if (!GestureMath.IsValidThreshold(threshold))
            {
                this.LogWarning(
                    $"Threshold must be a finite number of at least 0, got {threshold}. It is ignored."
                );
                return;
            }

            _click.SetThresholdDip(threshold);
        }

        public bool Enabled => _enabled;

        /// <summary>
        /// Disabling cancels a press at once: <see cref="IsPressed"/> turns false and the release won't click.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            if (enabled == _enabled)
                return;

            if (!enabled)
                CancelPress();

            _enabled = enabled;
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            if (!_enabled)
                return false;

            if (notification is PointerCancelNotification cancel)
            {
                OnCancelled(cancel);
                return false;
            }

            if (!PointerInput.TryRead(notification, out var sample))
                return false;

            if (_holdUntilUp)
            {
                // The press was cancelled while a gesture of this node holds the pointer: it stays pressed until the release.
                if (
                    sample.Kind == PointerSampleKind.Up
                    && sample.PointerId == _pointerId
                    && sample.Button == _button
                )
                    EndPress();
                return false;
            }

            var click = _click.Process(in sample);
            switch (click.Phase)
            {
                case ClickPhase.Pressed:
                    _pressed = true;
                    _pointerId = sample.PointerId;
                    _button = sample.Button;
                    _node = sample.Node;
                    _isPressed?.EnsureValue(true);
                    return false;
                case ClickPhase.Clicked:
                    EndPress();
                    _onClick?.EnsureValue(Unit.Default, force: true);
                    return true;
                case ClickPhase.Released:
                    EndPress();
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The press can't click any more. While a claim on the pressed node holds the pointer, the node is still pressed,
        /// so <see cref="IsPressed"/> waits for the release; otherwise it turns false now (O2).
        /// </summary>
        private void OnCancelled(PointerCancelNotification cancel)
        {
            if (!_pressed || cancel.PointerId != _pointerId || cancel.IsMember(this))
                return;

            _click.Cancel();
            if (cancel.Claim is { IsActive: true } claim && ReferenceEquals(claim.Node, _node))
            {
                _holdUntilUp = true;
                return;
            }

            EndPress();
        }

        private void CancelPress()
        {
            _click.Cancel();
            EndPress();
        }

        private void EndPress()
        {
            if (_pressed)
                _isPressed?.EnsureValue(false);

            _pressed = false;
            _holdUntilUp = false;
            _pointerId = 0;
            _button = MouseButtons.None;
            _node = null;
        }
    }
}
