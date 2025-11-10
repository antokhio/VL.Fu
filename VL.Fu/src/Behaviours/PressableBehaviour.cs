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
    /// A behavior that sends a Unit notification through a channel when it is pressed down.
    /// </summary>
    [ProcessNode(
        Name = "Pressable",
        HasStateOutput = true,
        FragmentSelection = FragmentSelection.Explicit
    )]
    public class Pressable : BehaviourBase, IInteractiveBehavior
    {
        private readonly ChannelProperty<Unit> _pressedChannel = new(new Unit());
        public override int Priority => BehaviourPriority.Click;

        private readonly Spread<IGesture> _gestures;

        [Fragment]
        public Pressable()
        {
            _gestures = new IGesture[] { new PointerDownGesture() }.ToSpread();
        }

        /// <summary>
        /// Allows an external channel to be provided from upstream.
        /// If a channel is provided, press notifications will be sent through it instead of the internal one.
        /// </summary>
        [Fragment(Order = PinOrder.Action)]
        public void SetChannel(IChannel<Unit>? channel) => _pressedChannel.SetChannel(channel);

        public IEnumerable<IGesture> GetGestures() => _gestures;

        /// <summary>
        /// When the PointerDownGesture matches, push a Unit value to the active channel.
        /// </summary>
        public void OnActivate(IGesture gesture)
        {
            _pressedChannel.OnNext(new Unit());
        }

        public void OnAdvance(GestureInputContext context) { }

        public void OnDeactivate() { }

        public void OnCancel() { }

        [Fragment]
        public IChannel<Unit> Channel => _pressedChannel;
    }
}
