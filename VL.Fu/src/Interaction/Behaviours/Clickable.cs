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
        public override bool IsTransient => false;

        // We default to Click Priority
        public override int Priority =>
            _priority.Value == GesturePriority.None ? GesturePriority.Click : _priority.Value;

        private readonly ChannelProperty<Unit> _clickedChannel = new(Unit.Default);
        private readonly ChannelProperty<bool> _isPressedChannel = new(false);
        private int _previousRevision = 0;

        [Fragment]
        public Clickable(NodeContext nodeContext)
            : base(nodeContext)
        {
            Gestures = [new LeftClickGesture(this), new TapGesture(this)];

            _previousRevision = _clickedChannel.Revision;
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetClickedChannel(IChannel<Unit>? channel)
        {
            _clickedChannel.SetChannel(
                channel,
                (
                    nextChannel =>
                    {
                        _previousRevision = _clickedChannel.Revision;
                    }
                )
            );
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetIsPressedChannel(IChannel<bool>? channel) =>
            _isPressedChannel.SetChannel(channel);

        // -- Lifecycle Hooks --

        public override void OnStart(IFuNode host, FuGestureEvent ev)
        {
            _isPressedChannel.EnsureValue(true);
        }

        public override void OnUpdate(IFuNode host, FuGestureEvent ev)
        {
            _isPressedChannel.EnsureValue(true);
        }

        public override void OnFinish(IFuNode host, FuGestureEvent ev)
        {
            _isPressedChannel.EnsureValue(false);
            _clickedChannel.OnNext(Unit.Default);
        }

        public override void OnCancel(IFuNode host, FuGestureEvent ev)
        {
            _isPressedChannel.EnsureValue(false);
        }

        [Fragment]
        public void Update(out bool isClick)
        {
            var currentRevision = _clickedChannel.Revision;
            isClick = _previousRevision != currentRevision;
            _previousRevision = currentRevision;
        }

        [Fragment]
        public bool IsPressed => _isPressedChannel.Value;
    }
}
