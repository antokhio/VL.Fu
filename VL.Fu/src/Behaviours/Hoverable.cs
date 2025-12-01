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

        [Fragment]
        public Hoverable()
        {
            _gestures = new IGesture[] { new PointerMoveGesture() }.ToSpread();
        }

        public override IEnumerable<IGesture> Gestures => _gestures;

        [Fragment(Order = PinOrder.Action)]
        public void SetIsHoveredChannel(IChannel<bool>? isHoveredChannel) =>
            _isHoveredChannel.SetChannel(isHoveredChannel);

        public void OnActivate(IGesture gesture)
        {
            _isHoveredChannel.OnNext(true);
        }

        public void OnAdvance(GestureInputContext context) { }

        public void OnDeactivate()
        {
            _isHoveredChannel.OnNext(false);
        }

        public void OnCancel()
        {
            _isHoveredChannel.OnNext(false);
        }

        /// <summary>
        /// A value that is true when the host is being hovered, and false otherwise.
        /// </summary>
        [Fragment]
        public bool IsHovered => _isHoveredChannel.Value;
    }
}
