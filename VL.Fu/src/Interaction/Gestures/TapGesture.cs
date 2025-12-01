using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Interaction.Gestures
{
    public class TapGesture : GestureBase
    {
        public override int Priority => GesturePriority.Click;

        public TapGesture(IFuBehaviour behaviour)
            : base(behaviour) { }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            // 1. Capture new pointers (Multi-touch claim)
            foreach (var pointer in candidates)
            {
                if (pointer.State == TouchNotificationKind.TouchDown)
                {
                    // Prevent duplicates
                    if (!_activators.Any(p => p.Id == pointer.Id))
                    {
                        _activators.Add(pointer);
                    }
                }
            }

            // 2. Update state of existing pointers
            for (int i = _activators.Count - 1; i >= 0; i--)
            {
                var tracked = _activators[i];
                if (inputState.Pointers.TryGetValue(tracked.Id, out var currPointer))
                {
                    _activators[i] = currPointer; // Update info

                    // Logic for completion/cancellation
                    if (currPointer.State == TouchNotificationKind.TouchUp)
                    {
                        // If it's the Primary pointer, we might Finish.
                        // If it's secondary, we just release it.
                        if (i == 0)
                        {
                            if (Host != null && Host.HitTest(currPointer))
                                Status = GestureStatus.Finish;
                            else
                                Status = GestureStatus.Cancel; // Primary released outside
                        }

                        // Stop tracking this specific pointer
                        _activators.RemoveAt(i);
                    }
                }
                else
                {
                    // Pointer lost
                    _activators.RemoveAt(i);
                }
            }

            // 3. Determine Global Status based on Primary Pointer
            if (_activators.Count > 0)
            {
                // If we just started or are still holding
                if (
                    Status == GestureStatus.Idle
                    || Status == GestureStatus.Finish
                    || Status == GestureStatus.Cancel
                )
                {
                    Status = GestureStatus.Start;
                }
                else
                {
                    Status = GestureStatus.Update;
                }
            }
            else
            {
                // If we were active but ran out of pointers, and didn't finish just now...
                if (Status != GestureStatus.Finish && Status != GestureStatus.Cancel)
                {
                    Status = GestureStatus.Idle;
                }
            }
        }
    }
}
