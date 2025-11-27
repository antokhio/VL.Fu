using Stride.Core.Mathematics;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Interaction.Gestures
{
    public class DragGesture : GestureBase
    {
        public override int Priority => GesturePriority.Drag;

        public Vector2 Delta { get; private set; }

        // Threshold primarily tuned for Normalized space (default).
        // If working in Pixel space, this might need to be adjusted or injected.
        private const float DragThreshold = 0.05f;

        private Vector2 _startPos;
        private Vector2 _lastPos;

        public DragGesture(IFuBehaviour behaviour)
            : base(behaviour) { }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            // 1. Capture Pointers (Multi-touch support to block fall-through)
            foreach (var pointer in candidates)
            {
                if (pointer.State == TouchNotificationKind.TouchDown)
                {
                    if (!_activators.Any(p => p.Id == pointer.Id))
                    {
                        _activators.Add(pointer);

                        // Initialize tracking if this is the first/primary pointer
                        if (_activators.Count == 1)
                        {
                            _startPos = pointer.Position;
                            _lastPos = pointer.Position;
                            Delta = Vector2.Zero;
                            Status = GestureStatus.Possible;
                        }
                    }
                }
            }

            // 2. Update Pointers
            for (int i = _activators.Count - 1; i >= 0; i--)
            {
                var tracked = _activators[i];
                if (inputState.Pointers.TryGetValue(tracked.Id, out var currPointer))
                {
                    _activators[i] = currPointer;

                    if (currPointer.State == TouchNotificationKind.TouchUp)
                    {
                        // If Primary pointer released
                        if (i == 0)
                        {
                            if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                                Status = GestureStatus.Finish;
                            else
                                Status = GestureStatus.Cancel; // Released before threshold
                        }
                        _activators.RemoveAt(i);
                    }
                }
                else
                {
                    _activators.RemoveAt(i); // Pointer lost
                    if (i == 0)
                        Status = GestureStatus.Cancel;
                }
            }

            // 3. Logic (Driven by Primary Pointer)
            if (
                _activators.Count > 0
                && Status != GestureStatus.Finish
                && Status != GestureStatus.Cancel
            )
            {
                var primary = _activators[0];

                if (Status == GestureStatus.Possible)
                {
                    var dist = (primary.Position - _startPos).Length();
                    if (dist > DragThreshold)
                    {
                        // Crossed Threshold -> Transition to Start
                        Status = GestureStatus.Start;

                        // Reset lastPos to current to avoid a "jump" equal to the threshold distance.
                        // Delta will be calculated starting from the NEXT movement.
                        _lastPos = primary.Position;
                        Delta = Vector2.Zero;
                    }
                }
                else if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                {
                    Status = GestureStatus.Update;

                    // Calculate delta for this frame
                    Delta = primary.Position - _lastPos;
                    _lastPos = primary.Position;
                }
            }
            else if (Status != GestureStatus.Finish && Status != GestureStatus.Cancel)
            {
                // No pointers left and didn't finish/cancel in this frame
                Status = GestureStatus.Idle;
            }
        }

        public override void Reset()
        {
            base.Reset();
            Delta = Vector2.Zero;
            _startPos = Vector2.Zero;
            _lastPos = Vector2.Zero;
        }
    }
}
