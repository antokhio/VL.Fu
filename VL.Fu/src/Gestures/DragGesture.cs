using Stride.Core.Mathematics;
using VL.Fu.Core.Gesture;
using VL.Fu.Extensions;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Gestures
{
    /// <summary>
    /// A gesture that recognizes a drag operation.
    /// It activates only after a pointer has moved more than a specified threshold distance
    /// from its initial down position.
    /// </summary>
    public class DragGesture : GestureBase
    {
        private Vector2 _startPosition;

        /// <summary>
        /// The distance in DIP unit that pointer must move before the drag is recognized.
        /// </summary>
        public float Threshold { get; set; } = 0.05f;

        /// <summary>
        /// Processes the input to detect the start of a drag.
        /// </summary>
        protected override void OnProcessInput(GestureInputContext context)
        {
            var pointer = context.PrimaryPointer;

            switch (Status)
            {
                case GestureStatus.Ready:
                    // A drag can only start with a TouchDown event.
                    if (pointer.State == TouchNotificationKind.TouchDown)
                    {
                        // The gesture is now possible. Record the starting position.
                        Status = GestureStatus.Possible;
                        _startPosition = pointer.Position;
                    }
                    else
                    {
                        // If the first event isn't a TouchDown, this gesture can't proceed.
                        Status = GestureStatus.Failed;
                    }
                    break;

                case GestureStatus.Possible:
                    // If the pointer is released before the threshold is met, the drag fails.
                    if (pointer.State == TouchNotificationKind.TouchUp)
                    {
                        Status = GestureStatus.Failed;
                        break;
                    }

                    var thresholdInCurrentSpace = this.ToCurrentSpace(
                        Threshold,
                        CommonSpace.DIPTopLeft
                    );

                    // Check if the pointer has moved beyond the threshold.
                    var distance = Vector2.Distance(_startPosition, pointer.Position);
                    if (distance > thresholdInCurrentSpace)
                    {
                        // The drag is confirmed.
                        Status = GestureStatus.Matched;

                        // The activation data is the pointer that initiated the drag.
                        ActivationData = pointer;
                    }
                    break;
            }
        }

        /// <summary>
        /// Resets the gesture back to its initial state.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _startPosition = Vector2.Zero;
        }
    }
}
