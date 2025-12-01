using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Property;
using VL.Fu.Interaction.Gestures;
using VL.Lib.Reactive;

namespace VL.Fu.Interaction.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class Hoverable : BehaviourBase, IFuBehaviour
    {
        // Transient behaviors run in parallel and do not participate in conflict resolution (blocking)
        public override bool IsTransient => true;

        [Fragment]
        public bool IsHovered => _isHoveredChannel.Value;

        private readonly ChannelProperty<bool> _isHoveredChannel = new(false);

        [Fragment]
        public Hoverable(NodeContext nodeContext)
            : base(nodeContext)
        {
            Gestures = [new PointerOverGesture(this)];
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetIsHoveredChannel(IChannel<bool>? isHoveredChannel) =>
            _isHoveredChannel.SetChannel(isHoveredChannel);

        // -- Lifecycle Hooks --

        public override void OnStart(IFuNode host, FuGestureEvent ev)
        {
            _isHoveredChannel.EnsureValue(true);
        }

        public override void OnUpdate(IFuNode host, FuGestureEvent ev)
        {
            _isHoveredChannel.EnsureValue(true);
        }

        public override void OnFinish(IFuNode host, FuGestureEvent ev)
        {
            _isHoveredChannel.EnsureValue(false);
        }

        public override void OnCancel(IFuNode host, FuGestureEvent ev)
        {
            _isHoveredChannel.EnsureValue(false);
        }
    }
}
