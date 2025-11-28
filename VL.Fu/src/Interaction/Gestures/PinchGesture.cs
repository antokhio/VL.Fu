using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Interaction.Gestures
{
    public class PinchGesture : GestureBase
    {
        // Higher than Drag, so Pinch cancels Drag
        public override int Priority => GesturePriority.Zoom;

        public PinchGesture(IFuBehaviour behaviour)
            : base(behaviour) { }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            // (Same logic as before: count >= 2)
            foreach (var p in candidates)
            {
                if (p.State == TouchNotificationKind.TouchDown)
                {
                    if (!_activators.Any(x => x.Id == p.Id))
                        _activators.Add(p);
                }
            }

            for (int i = _activators.Count - 1; i >= 0; i--)
            {
                var tracked = _activators[i];
                if (inputState.Pointers.TryGetValue(tracked.Id, out var curr))
                {
                    _activators[i] = curr;
                    if (curr.State == TouchNotificationKind.TouchUp)
                        _activators.RemoveAt(i);
                }
                else
                    _activators.RemoveAt(i);
            }

            if (_activators.Count >= 2)
            {
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
                if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                    Status = GestureStatus.Finish;
                else
                    Status = GestureStatus.Idle;
            }
        }
    }
}
