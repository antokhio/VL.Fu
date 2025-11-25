using VL.Core;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Interaction.Gestures
{
    public class ClickGesture : GestureBase
    {
        private int _trackedPointerId = -1;

        public ClickGesture(NodeContext nodeContext)
            : base(nodeContext) { }

        public override bool Match(
            IFuNode host,
            FuInputState inputState,
            out FuGestureEvent? gestureMatchedEvent
        )
        {
            gestureMatchedEvent = null;

            foreach (var pointer in inputState.Pointers.Values)
            {
                if (pointer.State == TouchNotificationKind.TouchDown && host.HitTest(pointer))
                {
                    _trackedPointerId = pointer.Id;
                    State = GestureState.Possible;
                    gestureMatchedEvent = new FuGestureEvent(this, pointer, inputState);
                    return true;
                }
            }
            return false;
        }

        public override bool Advance(
            IFuNode host,
            FuInputState inputState,
            out FuGestureEvent? gestureAdvancedEvent
        )
        {
            gestureAdvancedEvent = null;

            if (
                _trackedPointerId == -1
                || !inputState.Pointers.TryGetValue(_trackedPointerId, out var pointer)
            )
            {
                State = GestureState.Cancelled;
                return false;
            }

            if (pointer.State == TouchNotificationKind.TouchUp)
            {
                if (host.HitTest(pointer))
                {
                    State = GestureState.Matched; // Click confirmed!
                    gestureAdvancedEvent = new FuGestureEvent(this, pointer, inputState);
                    return false; // Interaction finished
                }
                else
                {
                    State = GestureState.Failed; // Released outside
                    return false;
                }
            }

            State = GestureState.Possible; // Still held down
            gestureAdvancedEvent = new FuGestureEvent(this, pointer, inputState);
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            _trackedPointerId = -1;
        }
    }
}
