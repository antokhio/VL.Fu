using VL.Fu.Core.Gesture;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Gestures
{
    /// <summary>
    /// A simple gesture that recognizes an immediate "move" event from a pointer.
    /// It matches as soon as a pointer's state is TouchMove on the target.
    /// It is intended for transient behaviors like hovering, which do not capture the pointer.
    /// </summary>
    public class PointerMoveGesture : GestureBase
    {
        protected override void OnProcessInput(GestureInputContext context)
        {
            // This gesture only cares about move events.
            if (Status == GestureStatus.Ready)
            {
                if (
                    context.PrimaryPointer.State == TouchNotificationKind.TouchMove
                    || context.PrimaryPointer.State == TouchNotificationKind.TouchDown
                )
                {
                    // The pattern is matched.
                    Status = GestureStatus.Matched;
                    ActivationData = context.PrimaryPointer;
                }
                else
                {
                    // If the event is not a TouchMove, this gesture fails for this cycle.
                    Status = GestureStatus.Failed;
                }
            }
        }
    }
}
