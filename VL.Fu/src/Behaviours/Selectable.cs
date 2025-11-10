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
    /// A behavior that allows an element to be selected or deselected.
    /// It maintains a boolean state and toggles it upon activation.
    /// </summary>
    [ProcessNode(Name = "Selectable", FragmentSelection = FragmentSelection.Explicit)]
    public class Selectable : BehaviourBase, IInteractiveBehavior
    {
        // The core state of the behavior: is it selected or not?
        private readonly ChannelProperty<bool> _isSelectedChannel = new(false);

        private readonly CachedProperty<float> _threshold = new(0.05f);
        private readonly TapGesture _tapGesture = new();

        public override int Priority => BehaviourPriority.Click;
        public override IEnumerable<IGesture> Gestures { get; }

        [Fragment]
        public Selectable()
        {
            // For now, selection happens on a simple pointer down.
            // This could be replaced with a more complex TapGesture later.
            Gestures = new IGesture[] { _tapGesture }.ToSpread();
        }

        /// <summary>
        /// Allows an external channel to be provided for two-way binding of the selection state.
        /// </summary>
        [Fragment(Order = PinOrder.Input)]
        public void SetIsSelectedChannel(IChannel<bool>? channel) =>
            _isSelectedChannel.SetChannel(channel);

        /// <summary>
        /// Sets threshold
        /// </summary>
        /// <param name="threshold">Threshold in DIP</param>
        [Fragment]
        public void SetThreshold(float threshold = 0.05f) =>
            _threshold.TrySetValue(threshold, (curr, next) => _tapGesture.Threshold = next);

        /// <summary>
        /// Outputs the current selection state.
        /// </summary>
        [Fragment]
        public bool IsSelected => _isSelectedChannel.Value;

        /// <summary>
        /// Called when the PointerDownGesture is matched.
        /// This toggles the selection state.
        /// </summary>
        public void OnActivate(IGesture gesture)
        {
            _isSelectedChannel.OnNext(!_isSelectedChannel.Value);
        }

        // This behavior does not need to do anything during Advance, Deactivate, or Cancel.
        public void OnAdvance(GestureInputContext context) { }

        public void OnDeactivate(GestureInputContext context) { }

        public void OnCancel() { }
    }
}
