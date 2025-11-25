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
        public IReadOnlyList<IFuGesture> Gestures { get; }
        public bool IsTransient => false;

        // Stores the event channel. Initialized with Default Unit.
        private readonly ChannelProperty<Unit> _clickedChannel = new(Unit.Default);
        private readonly ClickGesture _gesture;
        private int _previousRevision;

        [Fragment]
        public Clickable(NodeContext nodeContext)
            : base(nodeContext)
        {
            _gesture = new ClickGesture(nodeContext);
            Gestures = new[] { _gesture };

            _previousRevision = _clickedChannel.Revision;
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetClickedChannel(IChannel<Unit>? channel) =>
            _clickedChannel.SetChannel(channel);

        // Called on Touch Down
        public void OnActivate(IFuNode host, FuGestureEvent ev)
        {
            // Optional: You could set an "IsPressed" state here if needed visually
        }

        public void OnAdvance(IFuNode host, FuGestureEvent ev) { }

        // Called on Touch Up (End of interaction)
        public void OnDeactivate(IFuNode host, FuGestureEvent ev)
        {
            // Only fire the click if the gesture successfully Matched (Released inside bounds)
            if (ev.Gesture.State == GestureState.Matched)
            {
                _clickedChannel.OnNext(Unit.Default);
            }
        }

        public void OnCancel(IFuNode host) { }

        [Fragment(Order = PinOrder.Output)]
        public void Update(out bool isClick)
        {
            // Check if the channel was written to since the last frame
            var currentRevision = _clickedChannel.Revision;
            isClick = _previousRevision != currentRevision;
            _previousRevision = currentRevision;
        }
    }
}
