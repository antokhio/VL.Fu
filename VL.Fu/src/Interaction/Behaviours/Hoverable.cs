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
            Gestures =
            [
                new PointerOverGesture(
                    this,
                    onStart: g => _isHoveredChannel.EnsureValue(true),
                    onUpdate: g => _isHoveredChannel.EnsureValue(true),
                    onFinish: g => _isHoveredChannel.EnsureValue(false)
                ),
            ];
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetIsHoveredChannel(IChannel<bool>? isHoveredChannel) =>
            _isHoveredChannel.SetChannel(isHoveredChannel);

        // -- Lifecycle Hooks --
        // We only override OnCancel to handle external cancellations (e.g. whole system disabled)
        // Logic for Start/Update/Finish is handled by the gesture callbacks.

        public override void OnCancel(IFuNode host, FuGestureEvent ev)
        {
            _isHoveredChannel.EnsureValue(false);
        }
    }
}
