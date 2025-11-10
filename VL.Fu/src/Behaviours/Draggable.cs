using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Behaviour;
using VL.Fu.Core.Common;
using VL.Fu.Core.Gesture;
using VL.Fu.Core.Input;
using VL.Fu.Core.Property;
using VL.Fu.Gestures;
using VL.Lib.Collections;
using VL.Lib.Mathematics;
using VL.Lib.Reactive;

namespace VL.Fu.Behaviours
{
    /// <summary>
    /// A behavior that makes its host element draggable.
    /// It uses a DragGesture and outputs the new position of the element as the drag progresses.
    /// </summary>
    [ProcessNode(Name = "Draggable", FragmentSelection = FragmentSelection.Explicit)]
    public class Draggable : BehaviourBase, IInteractiveBehavior
    {
        private readonly CachedProperty<float> _threshold = new(0.05f);
        private readonly DragGesture _dragGesture = new();

        private readonly Spread<IGesture> _gestures;
        private readonly ChannelProperty<Vector2> _positionChannel = new(Vector2.Zero);

        private Vector2 _dragOffset;
        private readonly CachedProperty<Optional<Range<Vector2>>> _limits = new(new());

        public override int Priority => BehaviourPriority.Drag;

        [Fragment]
        public Draggable()
        {
            _gestures = new IGesture[] { _dragGesture }.ToSpread();
        }

        /// <summary>
        /// Optional limits for the drag position (e.g., to constrain movement within a parent panel).
        /// The range applies to the top-left corner of the dragged element.
        /// </summary>
        [Fragment]
        public void SetLimits(Optional<Range<Vector2>> limits) => _limits.TrySetValue(limits);

        /// <summary>
        /// Sets DragGesture threshold
        /// </summary>
        /// <param name="threshold">Threshold in DIP</param>
        [Fragment]
        public void SetThreshold(float threshold = 0.05f) =>
            _threshold.TrySetValue(threshold, (curr, next) => _dragGesture.Threshold = next);

        /// <summary>
        /// Outputs the position of the element as it's being dragged.
        /// </summary>
        [Fragment]
        public Vector2 Position => _positionChannel.Value;

        public override IEnumerable<IGesture> Gestures => _gestures;

        /// <summary>
        /// Providable postion Channel
        /// </summary>
        /// <param name="positionChannel">Providable postion Channel</param>
        [Fragment(Order = PinOrder.Input)]
        public void SetPostionChannel(IChannel<Vector2> positionChannel) =>
            _positionChannel.SetChannel(positionChannel);

        /// <summary>
        /// Called when the DragGesture is matched. This is the start of the drag.
        /// </summary>
        public void OnActivate(IGesture gesture)
        {
            if (
                gesture.ActivationData is not FuPointer pointer
                || Host?.Bounds is not RectangleF bounds
            )
                return;

            _dragOffset = pointer.Position - bounds.Center;
        }

        /// <summary>
        /// Called on every frame that the captured pointer moves.
        /// </summary>
        public void OnAdvance(GestureInputContext context)
        {
            var newPosition = context.PrimaryPointer.Position - _dragOffset;

            // Apply limits if they are provided
            if (_limits.Value.HasValue)
            {
                var limitRange = _limits.Value.Value;
                // Use the idiomatic VLMath.Clamp for vectors
                newPosition = VLMath.Clamp(newPosition, limitRange.From, limitRange.To);
            }

            _positionChannel.OnNext(newPosition);
        }

        /// <summary>
        /// Called when the pointer is released, ending the drag.
        /// </summary>
        public void OnDeactivate(GestureInputContext context)
        {
            // Nothing to do here for a simple drag.
            // A more complex implementation might send a "Drag Ended" event.
        }

        /// <summary>
        /// Called if the drag is interrupted.
        /// </summary>
        public void OnCancel()
        {
            // Nothing to do here. The element will just stop moving.
        }
    }
}
