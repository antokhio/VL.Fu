using System.Reactive;
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
    /// A behavior that sends a Unit notification through a channel when it is clicked.
    /// A click is defined as a pointer press and release within the bounds of the host.
    /// </summary>
    [ProcessNode(Name = "Clickable", FragmentSelection = FragmentSelection.Explicit)]
    public class Clickable : BehaviourBase, IInteractiveBehavior
    {
        private readonly ChannelProperty<Unit> _clickedChannel = new(new Unit());
        private readonly Spread<IGesture> _gestures;
        public override int Priority => BehaviourPriority.Click;

        [Fragment]
        public Clickable()
        {
            _gestures = new IGesture[] { new PointerClickGesture() }.ToSpread();
        }

        public override IEnumerable<IGesture> Gestures => _gestures;

        /// <summary>
        /// Allows an external channel to be provided from upstream.
        /// If a channel is provided, click notifications will be sent through it instead of the internal one.
        /// </summary>
        [Fragment(Order = PinOrder.Action)]
        public void SetClickedChannel(IChannel<Unit>? channel) =>
            _clickedChannel.SetChannel(channel);

        public void OnActivate(IGesture gesture)
        {
            _clickedChannel.OnNext(new Unit());
        }

        public void OnAdvance(GestureInputContext context) { }

        public void OnDeactivate() { }

        public void OnCancel() { }

        private int _previousRevision;

        [Fragment]
        public void Update(out bool isClick)
        {
            isClick = _previousRevision != _clickedChannel.Revision;
            _previousRevision = _clickedChannel.Revision;
        }
    }
}
