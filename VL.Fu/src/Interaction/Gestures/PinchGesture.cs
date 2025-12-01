using Stride.Core.Mathematics;
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

        /// <summary>
        /// The centroid (center point) between the two pointers.
        /// </summary>
        public Vector2 Position { get; private set; }

        /// <summary>
        /// The position of the primary pointer (first activator).
        /// </summary>
        public Vector2 StartPosition { get; private set; }

        /// <summary>
        /// The position of the secondary pointer (second activator).
        /// </summary>
        public Vector2 EndPosition { get; private set; }

        /// <summary>
        /// The distance between the two pointers.
        /// </summary>
        public float Distance { get; private set; }

        private readonly Action<PinchGesture>? _onStart;
        private readonly Action<PinchGesture>? _onUpdate;
        private readonly Action<PinchGesture>? _onFinish;
        private readonly Action<PinchGesture>? _onCancel;

        public PinchGesture(
            IFuBehaviour behaviour,
            Action<PinchGesture>? onStart = null,
            Action<PinchGesture>? onUpdate = null,
            Action<PinchGesture>? onFinish = null,
            Action<PinchGesture>? onCancel = null
        )
            : base(behaviour)
        {
            _onStart = onStart;
            _onUpdate = onUpdate;
            _onFinish = onFinish;
            _onCancel = onCancel;
        }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            // 1. Capture candidates (TouchDown)
            foreach (var p in candidates)
            {
                if (p.State == TouchNotificationKind.TouchDown)
                {
                    if (!_activators.Any(x => x.Id == p.Id))
                        _activators.Add(p);
                }
            }

            // 2. Update existing activators
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

            // 3. Logic
            if (_activators.Count >= 2)
            {
                // Map pointers to spatial properties
                StartPosition = _activators[0].Position;
                EndPosition = _activators[1].Position;

                // Calculate derived properties
                Position = (StartPosition + EndPosition) * 0.5f;
                Distance = (StartPosition - EndPosition).Length();

                if (
                    Status == GestureStatus.Idle
                    || Status == GestureStatus.Finish
                    || Status == GestureStatus.Cancel
                )
                {
                    Status = GestureStatus.Start;
                    _onStart?.Invoke(this);
                }
                else
                {
                    Status = GestureStatus.Update;
                    _onUpdate?.Invoke(this);
                }
            }
            else
            {
                if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                {
                    Status = GestureStatus.Finish;
                    _onFinish?.Invoke(this);
                }
                else
                {
                    Status = GestureStatus.Idle;
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            Position = Vector2.Zero;
            StartPosition = Vector2.Zero;
            EndPosition = Vector2.Zero;
            Distance = 0f;
        }
    }
}
