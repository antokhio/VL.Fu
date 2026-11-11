using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Behaviors
{
    /// <summary>
    /// Tells whether the mouse or the primary touch is over the node it is attached to.
    /// Attach it to a Fu node's behaviors and connect a channel to <see cref="IsHovered"/>.
    /// </summary>
    /// <remarks>
    /// Hover follows the pointer's hover path (v2 §3.5): a node is hovered while the pointer is over it or over one of its children,
    /// and while a gesture on the node holds the pointer. Pressing, cancelled gestures and claims don't change it.
    /// It never reports a notification as processed.
    /// </remarks>
    [ProcessNode(
        Name = "Hoverable",
        HasStateOutput = true,
        FragmentSelection = FragmentSelection.Explicit
    )]
    public sealed class Hoverable : NodeBase, IBehavior
    {
        private IChannel<bool>? _isHovered = null;
        private bool _enabled = true;
        private bool _hovered = false;

        [Fragment]
        public Hoverable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// Receives true when the pointer enters the node and false when it leaves.
        /// </summary>
        public IChannel<bool>? IsHovered => _isHovered;

        /// <summary>
        /// Connects the channel that receives the hover state. A new channel gets the current state right away,
        /// and the previous one is told the pointer left, so nothing stays hovered on a channel that is no longer listened to.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetIsHovered(IChannel<bool>? isHovered)
        {
            if (ReferenceEquals(isHovered, _isHovered))
                return;

            if (_enabled && _hovered)
                _isHovered?.EnsureValue(false);

            _isHovered = isHovered;

            if (_enabled && _hovered)
                _isHovered?.EnsureValue(true);
        }

        public bool Enabled => _enabled;

        /// <summary>
        /// Disabling writes false to <see cref="IsHovered"/>. The node keeps following the pointer meanwhile,
        /// so enabling again writes whether it is over the node now.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            if (enabled == _enabled)
                return;

            _enabled = enabled;
            if (_hovered)
                _isHovered?.EnsureValue(enabled);
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            switch (notification)
            {
                case PointerEnterNotification:
                    _hovered = true;
                    if (_enabled)
                        _isHovered?.EnsureValue(true);
                    break;
                case PointerLeaveNotification:
                    _hovered = false;
                    if (_enabled)
                        _isHovered?.EnsureValue(false);
                    break;
            }

            return false;
        }
    }
}
