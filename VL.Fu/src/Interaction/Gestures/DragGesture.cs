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

        // Threshold primarily tuned for Normalized space (default).
        private const float DragThreshold = 0.05f;

        private Vector2 _startPos;

        public DragGesture(IFuBehaviour behaviour)
            : base(behaviour) { }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            // 1. Capture Pointers
            foreach (var pointer in candidates)
            {
                if (pointer.State == TouchNotificationKind.TouchDown)
                {
                    if (!_activators.Any(p => p.Id == pointer.Id))
                    {
                        _activators.Add(pointer);

                        if (_activators.Count == 1)
                        {
                            _startPos = pointer.Position;
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
                        if (i == 0)
                        {
                            if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                                Status = GestureStatus.Finish;
                            else
                                Status = GestureStatus.Cancel;
                        }
                        _activators.RemoveAt(i);
                    }
                }
                else
                {
                    _activators.RemoveAt(i);
                    if (i == 0)
                        Status = GestureStatus.Cancel;
                }
            }

            // 3. Logic
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
                        Status = GestureStatus.Start;
                        // Note: We don't track lastPos here anymore.
                        // The behaviour will pick up the current position at OnStart.
                    }
                }
                else if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                {
                    Status = GestureStatus.Update;
                }
            }
            else if (Status != GestureStatus.Finish && Status != GestureStatus.Cancel)
            {
                Status = GestureStatus.Idle;
            }
        }

        public override void Reset()
        {
            base.Reset();
            _startPos = Vector2.Zero;
        }
    }
}
