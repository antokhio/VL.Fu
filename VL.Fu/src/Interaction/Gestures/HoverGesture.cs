using VL.Core;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Interaction.Gestures
{
    public class HoverGesture : ContextConsumer, IFuGesture
    {
        public HoverGesture(NodeContext nodeContext)
            : base(nodeContext) { }

        public GestureState State { get; private set; } = GestureState.Ready;

        public bool Match(
            IFuNode host,
            FuInputState inputState,
            out FuGestureEvent? gestureMatchedEvent
        )
        {
            gestureMatchedEvent = null;

            foreach (var pointer in inputState.Pointers.Values)
            {
                if (pointer.State == TouchNotificationKind.TouchUp)
                    continue;

                if (host.HitTest(pointer))
                {
                    State = GestureState.Possible;
                    gestureMatchedEvent = new FuGestureEvent(this, pointer, inputState);
                    return true;
                }
            }

            return false;
        }

        public bool Advance(
            IFuNode host,
            FuInputState inputState,
            out FuGestureEvent? gestureAdvancedEvent
        )
        {
            gestureAdvancedEvent = null;

            bool isOver = false;
            object? activator = null;

            foreach (var pointer in inputState.Pointers.Values)
            {
                if (pointer.State != TouchNotificationKind.TouchUp && host.HitTest(pointer))
                {
                    isOver = true;
                    activator = pointer;
                    break;
                }
            }

            if (isOver)
            {
                State = GestureState.Possible;
                gestureAdvancedEvent = new FuGestureEvent(this, activator, inputState);
                return true;
            }

            State = GestureState.Failed;
            return false;
        }

        public void Reset() => State = GestureState.Ready;
    }
}
