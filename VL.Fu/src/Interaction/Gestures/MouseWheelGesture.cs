using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Interaction.Gestures
{
    public class MouseWheelGesture : GestureBase
    {
        public override int Priority => GesturePriority.Zoom;

        public MouseWheelGesture(IFuBehaviour behaviour)
            : base(behaviour) { }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            // 1. Check if Mouse is over the host (in candidates)
            bool isHovering = false;
            foreach (var p in candidates)
            {
                if (p.Id == Constants.MousePointerId)
                {
                    isHovering = true;
                    // We map the mouse pointer as the activator
                    if (!_activators.Contains(p))
                        _activators.Add(p);
                    break;
                }
            }

            if (!isHovering)
            {
                _activators.Clear();
                if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                    Status = GestureStatus.Finish;
                else
                    Status = GestureStatus.Idle;
                return;
            }

            // 2. Check Wheel Delta
            // We monitor Y (Vertical) wheel for standard zoom
            bool hasDelta = inputState.Mouse.WheelDelta.Y != 0;

            if (hasDelta)
            {
                // Update the activator with fresh state (WheelDelta is in the Mouse struct, not Pointer)
                // But logic usually reads inputState.Mouse directly in behavior.

                if (
                    Status == GestureStatus.Idle
                    || Status == GestureStatus.Finish
                    || Status == GestureStatus.Cancel
                )
                    Status = GestureStatus.Start;
                else
                    Status = GestureStatus.Update;
            }
            else
            {
                // No movement this frame
                if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                    Status = GestureStatus.Finish;
                else
                    Status = GestureStatus.Idle;
            }
        }
    }
}
