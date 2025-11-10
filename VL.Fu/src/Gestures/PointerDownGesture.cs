using VL.Fu.Core.Gesture;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Gestures
{
    /// <summary>
    /// A simple gesture that recognizes an immediate "down" event from a pointer.
    /// It matches as soon as a pointer's state is TouchDown on the target.
    /// </summary>
    public class PointerDownGesture : GestureBase
    {
        /// <summary>
        /// Processes the input to check for a TouchDown event.
        /// </summary>
        protected override void OnProcessInput(GestureInputContext context)
        {
            // This gesture only cares about the very first event it receives.
            // If the gesture is in the 'Ready' state and the pointer is in the 'TouchDown' state, we have a match.
            if (
                Status == GestureStatus.Ready
                && context.PrimaryPointer.State == TouchNotificationKind.TouchDown
            )
            {
                // The pattern is matched.
                Status = GestureStatus.Matched;

                // The activation data is the pointer that triggered the gesture.
                ActivationData = context.PrimaryPointer;
            }
            else
            {
                // If the first event is not a TouchDown (e.g., an orphaned TouchMove), this gesture fails.
                Status = GestureStatus.Failed;
            }
        }
    }
}
