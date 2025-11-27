using System.Reactive;
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
    public class Clickable : BehaviourBase, IFuBehaviour
    {
        public bool IsTransient => false;

        private readonly ChannelProperty<Unit> _clickedChannel = new(Unit.Default);
        private readonly ChannelProperty<bool> _isPressedChannel = new(false);
        private readonly ClickGesture _gesture;
        private int _previousRevision = 0;

        [Fragment]
        public Clickable(NodeContext nodeContext)
            : base(nodeContext)
        {
            _gesture = new ClickGesture(nodeContext);
            Gestures = new[] { _gesture };

            // Initialize revision to match starting state to prevent start-up bang
            _previousRevision = _clickedChannel.Revision;
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetClickedChannel(IChannel<Unit>? channel)
        {
            _clickedChannel.SetChannel(channel);
            _previousRevision = _clickedChannel.Revision;
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetIsPressedChannel(IChannel<bool>? channel) =>
            _isPressedChannel.SetChannel(channel);

        public override void OnStart(IFuNode host, FuGestureEvent ev) =>
            _isPressedChannel.EnsureValue(true);

        public override void OnUpdate(IFuNode host, FuGestureEvent ev) =>
            _isPressedChannel.EnsureValue(true);

        public override void OnStop(IFuNode host, FuGestureEvent ev, bool isSuccess)
        {
            _isPressedChannel.EnsureValue(false); // Always release press

            if (isSuccess)
            {
                // No more checking ev.State! The Service guaranteed this is a Success.
                _clickedChannel.OnNext(Unit.Default);
            }
        }

        public override void OnCancel(IFuNode host) => _isPressedChannel.EnsureValue(false);

        [Fragment]
        public void Update(out bool isClick, out bool isPressed)
        {
            isPressed = _isPressedChannel.Value;

            var currentRevision = _clickedChannel.Revision;
            isClick = _previousRevision != currentRevision;
            _previousRevision = currentRevision;
        }
    }
}
