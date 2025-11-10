using Stride.Core.Mathematics;
using VL.Fu.Core.Gesture;
using VL.Fu.Extensions;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Gestures
{
    /// <summary>
    /// A gesture that recognizes a tap or click.
    /// It succeeds only when a pointer goes down and then up without moving beyond a specified threshold.
    /// </summary>
    public class TapGesture : GestureBase
    {
        private Vector2 _startPosition;

        /// <summary>
        /// The distance in DIPs the pointer can move before the tap is considered invalid.
        /// </summary>
        public float Threshold { get; set; } = 0.05f;

        protected override void OnProcessInput(GestureInputContext context)
        {
            var pointer = context.PrimaryPointer;
            var thresholdInCurrentSpace = this.ToCurrentSpace(Threshold, CommonSpace.DIP);

            switch (Status)
            {
                case GestureStatus.Ready:
                    // A tap can only start with a TouchDown event.
                    if (pointer.State == TouchNotificationKind.TouchDown)
                    {
                        Status = GestureStatus.Possible;
                        _startPosition = pointer.Position;
                    }
                    else
                    {
                        Status = GestureStatus.Failed;
                    }
                    break;

                case GestureStatus.Possible:
                    if (pointer.State == TouchNotificationKind.TouchMove)
                    {
                        // If the pointer moves too far, this is no longer a tap. It's a drag.
                        var distance = Vector2.Distance(_startPosition, pointer.Position);
                        if (distance > thresholdInCurrentSpace)
                        {
                            Status = GestureStatus.Failed;
                        }
                    }
                    else if (pointer.State == TouchNotificationKind.TouchUp)
                    {
                        // The pointer was released without moving significantly. Success!
                        Status = GestureStatus.Matched;
                        ActivationData = pointer;
                    }
                    break;
            }
        }

        public override void Reset()
        {
            base.Reset();
            _startPosition = Vector2.Zero;
        }
    }
}
