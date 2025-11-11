using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Behaviour;
using VL.Fu.Core.Common;
using VL.Fu.Core.Gesture;
using VL.Fu.Core.Property;
using VL.Fu.Gestures;
using VL.Lib.Collections;
using VL.Lib.Reactive;

namespace VL.Fu.Behaviours
{
    /// <summary>
    /// A transient behavior that detects when a pointer is hovering over its host.
    /// It does not capture the pointer.
    /// </summary>
    [ProcessNode(Name = "Hoverable", FragmentSelection = FragmentSelection.Explicit)]
    public class Hoverable : BehaviourBase, IInteractiveBehavior
    {
        private readonly ChannelProperty<bool> _isHoveredChannel = new(false);
        private readonly Spread<IGesture> _gestures;
        public override int Priority => BehaviourPriority.Hover;
        public override bool IsTransient => true;
        public override IEnumerable<IGesture> Gestures => Spread<IGesture>.Empty;

        [Fragment]
        public Hoverable()
        {
            _gestures = new IGesture[] { new PointerMoveGesture() }.ToSpread();
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetIsHoveredChannel(IChannel<bool>? isHoveredChannel) =>
            _isHoveredChannel.SetChannel(isHoveredChannel);

        /// <summary>
        /// Called by the InteractionService when a pointer moves. For transient behaviors,
        /// this is called when a pointer is over the host, regardless of capture state.
        /// </summary>
        public void OnAdvance(GestureInputContext context) { }

        /// <summary>
        /// Called by the InteractionService when the pointer is no longer considered to be
        /// hovering over the host (e.g., it has moved off or was released).
        /// </summary>
        public void OnCancel()
        {
            _isHoveredChannel.OnNext(false);
        }

        public void OnActivate(IGesture gesture)
        {
            _isHoveredChannel.OnNext(true);
        }

        public void OnDeactivate()
        {
            _isHoveredChannel.OnNext(false);
        }

        /// <summary>
        /// A channel that is true when the host is being hovered, and false otherwise.
        /// </summary>
        [Fragment]
        public bool IsHovered => _isHoveredChannel.Value;
    }
}
