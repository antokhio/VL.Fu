using Stride.Core.Mathematics;
using VL.Core;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Interaction.Gestures
{
    public class DragGesture : GestureBase
    {
        public Vector2 Delta { get; private set; }
        public bool HasStartedDragging { get; private set; }

        private int? _trackedPointerId = null;
        private Vector2 _lastPos;
        private Vector2 _startPos;

        // Threshold in DIPs
        private const float DragThreshold = 0.05f;

        public DragGesture(NodeContext nodeContext)
            : base(nodeContext) { }

        public override FuGestureState Match(IFuNode host, FuInputState inputState)
        {
            if (Phase != GesturePhase.Idle)
                return FuGestureState.Idle;

            foreach (var pointer in inputState.Pointers.Values)
            {
                if (pointer.State == TouchNotificationKind.TouchDown && host.HitTest(pointer))
                {
                    _trackedPointerId = pointer.Id;
                    _startPos = pointer.Position;
                    _lastPos = pointer.Position;
                    HasStartedDragging = false;
                    Delta = Vector2.Zero;

                    // Start in 'Possible' state. We are tracking, but haven't committed.
                    Phase = GesturePhase.Possible;
                    return FuGestureState.Possible(new FuGestureEvent(this, pointer, inputState));
                }
            }
            return FuGestureState.Idle;
        }

        public override FuGestureState Advance(IFuNode host, FuInputState inputState)
        {
            if (
                _trackedPointerId == null
                || !inputState.Pointers.TryGetValue(_trackedPointerId.Value, out var pointer)
            )
            {
                Phase = GesturePhase.Cancelled;
                return FuGestureState.Cancel();
            }

            var currentPos = pointer.Position;
            var frameDelta = currentPos - _lastPos;
            _lastPos = currentPos;
            Delta = frameDelta;

            var evt = new FuGestureEvent(this, pointer, inputState);

            // Check threshold logic
            if (!HasStartedDragging)
            {
                var dist = (currentPos - _startPos).Length();

                if (dist > DragThreshold)
                {
                    HasStartedDragging = true;

                    // COMMITMENT MOMENT: Transition to Began.
                    // This will signal the Service to cancel competing gestures (like Click).
                    Phase = GesturePhase.Began;
                    return FuGestureState.Began(evt);
                }
                else
                {
                    if (pointer.State == TouchNotificationKind.TouchUp)
                    {
                        // Released before threshold -> Failed (didn't drag)
                        Phase = GesturePhase.Failed;
                        return FuGestureState.Fail(evt);
                    }

                    // Still waiting for threshold
                    Phase = GesturePhase.Possible;
                    return FuGestureState.Possible(evt);
                }
            }
            else
            {
                // Already dragging
                if (pointer.State == TouchNotificationKind.TouchUp)
                {
                    Phase = GesturePhase.Matched;
                    return FuGestureState.Matched(evt);
                }

                Phase = GesturePhase.Changed;
                return FuGestureState.Changed(evt);
            }
        }

        public override void Reset()
        {
            base.Reset();
            _trackedPointerId = null;
            HasStartedDragging = false;
            Delta = Vector2.Zero;
        }
    }
}
