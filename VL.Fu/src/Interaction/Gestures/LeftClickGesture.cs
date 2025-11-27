using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Interaction.Gestures
{
    public class LeftClickGesture : GestureBase
    {
        public override int Priority => GesturePriority.Click;

        public LeftClickGesture(IFuBehaviour behaviour)
            : base(behaviour) { }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            // 1. Capture
            foreach (var pointer in candidates)
            {
                if (pointer.State == TouchNotificationKind.TouchDown && pointer.IsLeft)
                {
                    if (!_activators.Any(p => p.Id == pointer.Id))
                        _activators.Add(pointer);
                }
            }

            // 2. Update
            for (int i = _activators.Count - 1; i >= 0; i--)
            {
                var tracked = _activators[i];
                if (inputState.Pointers.TryGetValue(tracked.Id, out var currPointer))
                {
                    _activators[i] = currPointer;

                    if (currPointer.State == TouchNotificationKind.TouchUp)
                    {
                        if (i == 0)
                        {
                            if (Host != null && Host.HitTest(currPointer))
                                Status = GestureStatus.Finish;
                            else
                                Status = GestureStatus.Cancel;
                        }
                        _activators.RemoveAt(i);
                    }
                    else if (!currPointer.IsLeft)
                    {
                        if (i == 0)
                            Status = GestureStatus.Cancel;
                        _activators.RemoveAt(i);
                    }
                }
                else
                {
                    _activators.RemoveAt(i);
                }
            }

            // 3. Status
            if (_activators.Count > 0)
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
            else if (Status != GestureStatus.Finish && Status != GestureStatus.Cancel)
            {
                Status = GestureStatus.Idle;
            }
        }
    }
}
