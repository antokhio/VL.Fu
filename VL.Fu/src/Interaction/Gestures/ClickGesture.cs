using VL.Core;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Interaction.Gestures
{
    public class ClickGesture : GestureBase
    {
        private int? _trackedPointerId = null;

        public ClickGesture(NodeContext nodeContext)
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

                    // Start tracking in Possible state
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

            var evt = new FuGestureEvent(this, pointer, inputState);

            if (pointer.State == TouchNotificationKind.TouchUp)
            {
                if (inputState.IsFocused && host.HitTest(pointer))
                {
                    Phase = GesturePhase.Matched;
                    return FuGestureState.Matched(evt);
                }
                else
                {
                    Phase = GesturePhase.Failed;
                    return FuGestureState.Fail(evt);
                }
            }

            // Continue tracking (Possible)
            Phase = GesturePhase.Possible;
            return FuGestureState.Possible(evt);
        }

        public override void Reset()
        {
            base.Reset();
            _trackedPointerId = null;
        }
    }
}
