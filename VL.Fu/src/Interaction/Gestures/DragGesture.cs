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
        public float Threshold { get; set; } = 0.05f;

        public Vector2 StartPosition { get; private set; }
        public Vector2 CurrentPosition { get; private set; }

        // Callbacks
        private readonly Action<DragGesture>? _onStart;
        private readonly Action<DragGesture>? _onUpdate;
        private readonly Action<DragGesture>? _onFinish;

        public DragGesture(
            IFuBehaviour behaviour,
            Action<DragGesture>? onStart = null,
            Action<DragGesture>? onUpdate = null,
            Action<DragGesture>? onFinish = null
        )
            : base(behaviour)
        {
            _onStart = onStart;
            _onUpdate = onUpdate;
            _onFinish = onFinish;
        }

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
                            StartPosition = pointer.Position;
                            CurrentPosition = pointer.Position;
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
                            // Update final position before finishing
                            CurrentPosition = currPointer.Position;

                            if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                            {
                                Status = GestureStatus.Finish;
                                _onFinish?.Invoke(this);
                            }
                            else
                            {
                                Status = GestureStatus.Cancel;
                            }
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
                CurrentPosition = primary.Position;

                if (Status == GestureStatus.Possible)
                {
                    var dist = (primary.Position - StartPosition).Length();
                    if (dist > Threshold)
                    {
                        Status = GestureStatus.Start;
                        _onStart?.Invoke(this);
                    }
                }
                else if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                {
                    Status = GestureStatus.Update;
                    _onUpdate?.Invoke(this);
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
            StartPosition = Vector2.Zero;
            CurrentPosition = Vector2.Zero;
        }
    }
}
