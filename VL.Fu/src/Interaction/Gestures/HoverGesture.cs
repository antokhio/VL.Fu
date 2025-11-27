using VL.Core;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Interaction.Gestures
{
    public class HoverGesture : GestureBase
    {
        public HoverGesture(NodeContext nodeContext)
            : base(nodeContext) { }

        public override FuGestureState Match(IFuNode host, FuInputState inputState)
        {
            if (Phase != GesturePhase.Idle)
                return FuGestureState.Idle;

            foreach (var pointer in inputState.Pointers.Values)
            {
                if (pointer.State == TouchNotificationKind.TouchUp)
                    continue;

                if (host.HitTest(pointer))
                {
                    // Hover is immediately active.
                    // Using 'Began' here is fine, but since Hover doesn't capture pointers,
                    // it won't trigger conflict resolution against Click/Drag.
                    Phase = GesturePhase.Began;
                    return FuGestureState.Began(new FuGestureEvent(this, pointer, inputState));
                }
            }

            return FuGestureState.Idle;
        }

        public override FuGestureState Advance(IFuNode host, FuInputState inputState)
        {
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
                Phase = GesturePhase.Changed;
                return FuGestureState.Changed(new FuGestureEvent(this, activator!, inputState));
            }

            Phase = GesturePhase.Failed;
            return FuGestureState.Fail();
        }
    }
}
