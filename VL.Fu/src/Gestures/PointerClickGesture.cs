using VL.Fu.Core.Gesture;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Gestures
{
    /// <summary>
    /// A gesture that recognizes a click operation.
    /// It activates only when a pointer is released (TouchUp) inside the bounds
    /// of the IInteractiveHost that initiated the gesture.
    /// </summary>
    public class PointerClickGesture : GestureBase
    {
        protected override void OnProcessInput(GestureInputContext context)
        {
            var pointer = context.PrimaryPointer;

            // The Host is now a property of the gesture itself.
            if (Host is null)
            {
                Status = GestureStatus.Failed;
                return;
            }

            switch (Status)
            {
                case GestureStatus.Ready:
                    // A click can only start with a TouchDown event.
                    if (pointer.State == TouchNotificationKind.TouchDown)
                    {
                        Status = GestureStatus.Possible;
                    }
                    else
                    {
                        Status = GestureStatus.Failed;
                    }
                    break;

                case GestureStatus.Possible:
                    // While possible, the gesture can either fail or match.
                    if (pointer.State == TouchNotificationKind.TouchUp)
                    {
                        // On TouchUp, check if the pointer is still inside the host's bounds.
                        if (Host.HitTest(pointer))
                        {
                            Status = GestureStatus.Matched;
                            ActivationData = pointer;
                        }
                        else
                        {
                            // Pointer was released outside the bounds.
                            Status = GestureStatus.Failed;
                        }
                    }
                    else if (pointer.State == TouchNotificationKind.TouchMove)
                    {
                        // If the pointer moves outside the bounds before release, the click is invalid.
                        if (!Host.HitTest(pointer))
                        {
                            Status = GestureStatus.Failed;
                        }
                        // Otherwise, it remains possible.
                    }
                    break;
            }
        }
    }
}
