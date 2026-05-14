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

        private IDisposable _subscription;
        private bool _isClick = false;

        [Fragment]
        public Clickable(NodeContext nodeContext)
            : base(nodeContext)
        {
            // Shared handlers
            void OnStart(GestureBase g) => _isPressedChannel.EnsureValue(true);
            void OnUpdate(GestureBase g) => _isPressedChannel.EnsureValue(true);
            void OnFinish(GestureBase g)
            {
                _isPressedChannel.EnsureValue(false);
                _clickedChannel.OnNext(Unit.Default);
            }
            void OnCancel(GestureBase g) => _isPressedChannel.EnsureValue(false);

            Gestures =
            [
                new LeftClickGesture(this, OnStart, OnUpdate, OnFinish, OnCancel),
                new TapGesture(this, OnStart, OnUpdate, OnFinish, OnCancel),
            ];

            _subscription = _clickedChannel.Subscribe(_ => _isClick = true);
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetClickedChannel(IChannel<Unit> channel)
        {
            _clickedChannel.SetChannel(channel);

            //_clickedChannel.SetChannel(
            //    channel,
            //    (
            //        nextChannel =>
            //        {
            //            _previousRevision = _clickedChannel.Revision;
            //        }
            //    )
            //);
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetIsPressedChannel(IChannel<bool>? channel) =>
            _isPressedChannel.SetChannel(channel);

        // -- Lifecycle Hooks --

        // OnCancel handles external cancellation (from InteractionService conflict resolution)
        // We rely on the Gestures to handle internal cancellation (moving mouse off element)
        public override void OnCancel(IFuNode host, FuGestureEvent ev)
        {
            _isPressedChannel.EnsureValue(false);
        }

        [Fragment]
        public void Update(out bool isClick)
        {
            isClick = _isClick;

            _isClick = false;
        }

        [Fragment]
        public bool IsPressed => _isPressedChannel.Value;
    }
}
