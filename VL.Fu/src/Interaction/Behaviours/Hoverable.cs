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
        public IReadOnlyList<IFuGesture> Gestures { get; }
        public bool IsTransient => true;

        [Fragment]
        public bool IsHovered => _isHoveredChannel.Value;

        private readonly ChannelProperty<bool> _isHoveredChannel = new(false);
        private readonly HoverGesture _gesture;

        [Fragment]
        public Hoverable(NodeContext nodeContext)
            : base(nodeContext)
        {
            _gesture = new HoverGesture(nodeContext);
            Gestures = new[] { _gesture };
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetIsHoveredChannel(IChannel<bool>? isHoveredChannel) =>
            _isHoveredChannel.SetChannel(isHoveredChannel);

        public void OnActivate(IFuNode host, FuGestureEvent ev) =>
            _isHoveredChannel.EnsureValue(true);

        public void OnAdvance(IFuNode host, FuGestureEvent ev) =>
            _isHoveredChannel.EnsureValue(true);

        public void OnDeactivate(IFuNode host, FuGestureEvent ev) =>
            _isHoveredChannel.EnsureValue(false);

        public void OnCancel(IFuNode host) => _isHoveredChannel.EnsureValue(false);
    }
}
