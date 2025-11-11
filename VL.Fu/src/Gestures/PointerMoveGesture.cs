using VL.Fu.Core.Gesture;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Gestures
{
    /// <summary>
    /// A simple gesture that recognizes any pointer "down" or "move" event.
    /// It's intended for transient behaviors like Hover, where the goal is to react to
    /// the pointer's presence without a complex recognition pattern.
    /// </summary>
    public class PointerMoveGesture : GestureBase
    {
        protected override void OnProcessInput(GestureInputContext context)
        {
            var pointerState = context.PrimaryPointer.State;

            if (
                pointerState == TouchNotificationKind.TouchDown
                || pointerState == TouchNotificationKind.TouchMove
            )
            {
                // The pattern is matched as long as the pointer is active over the target.
                Status = GestureStatus.Matched;
                ActivationData = context.PrimaryPointer;
            }
            else if (pointerState == TouchNotificationKind.TouchUp)
            {
                // The interaction is over, reset for the next time.
                Reset();
            }
        }
    }
}
