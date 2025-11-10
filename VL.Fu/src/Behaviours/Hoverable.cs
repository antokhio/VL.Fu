using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Gesture;
using VL.Fu.Core.Property;
using VL.Fu.Gestures;
using VL.Lib.Collections;

namespace VL.Fu.Behaviours
{
    /// <summary>
    /// A transient behavior that detects when a pointer is hovering over its host.
    /// It outputs a boolean state and does not capture the pointer, allowing other behaviors to execute.
    /// </summary>
    [ProcessNode(
        Name = "Hoverable",
        HasStateOutput = true,
        FragmentSelection = FragmentSelection.Explicit
    )]
    public class Hoverable : BehaviourBase
    {
        private readonly ChannelProperty<bool> _isHoveredChannel = new(false);
        private readonly HoverGesture _hoverGesture = new();

        // This behavior has a low priority as it's a passive state.
        public override int Priority => BehaviourPriority.Hover;

        public override bool IsTransient => true;

        public override IEnumerable<IGesture> Gestures { get; }

        [Fragment]
        public Hoverable()
        {
            Gestures = new IGesture[] { _hoverGesture }.ToSpread();
        }

        [Fragment(Order = PinOrder.Output)]
        public bool IsHovered => _isHoveredChannel.Value;

        /// <summary>
        /// Called when the HoverGesture matches (pointer enters).
        /// </summary>
        public void OnActivate(IGesture gesture)
        {
            _isHoveredChannel.OnNext(true);
            // After activating, we immediately reset the gesture so it's ready to detect an exit.
            gesture.Reset();
        }

        /// <summary>
        /// This behavior is passive and does not need to do anything on Advance.
        /// The gesture's state is checked on every pointer move by the InteractionService.
        /// </summary>
        public void OnAdvance(GestureInputContext context)
        {
            // The InteractionService checks all active gestures on move. If our HoverGesture
            // transitions to Failed (meaning an exit), we need to react.
            if (_hoverGesture.Status == GestureStatus.Failed)
            {
                _isHoveredChannel.OnNext(false);
                _hoverGesture.Reset();
            }
        }

        /// <summary>
        /// This is called if the interaction is fully cancelled (e.g., pointer leaves window).
        /// </summary>
        public void OnDeactivate(GestureInputContext context)
        {
            _isHoveredChannel.OnNext(false);
            _hoverGesture.ForceExit();
        }

        public void OnCancel()
        {
            OnDeactivate(default);
        }
    }
}
