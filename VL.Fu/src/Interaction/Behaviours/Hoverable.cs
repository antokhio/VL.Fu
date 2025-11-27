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

        // Hover is transient (doesn't capture input, allows bubbling)
        public override bool IsTransient => true;

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

        public override void OnStart(IFuNode host, FuGestureEvent ev)
        {
            _isHoveredChannel.EnsureValue(true);
        }

        public override void OnUpdate(IFuNode host, FuGestureEvent ev)
        {
            _isHoveredChannel.EnsureValue(true);
        }

        public override void OnStop(IFuNode host, FuGestureEvent ev, bool isSuccess)
        {
            // Whether it failed (moved out) or matched (rare for hover), reset state
            _isHoveredChannel.EnsureValue(false);
        }

        public override void OnCancel(IFuNode host)
        {
            _isHoveredChannel.EnsureValue(false);
        }
    }
}
