using System.Collections.Immutable;
using Stride.Core.Mathematics;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Interaction.Gestures
{
    public class MouseWheelGesture : GestureBase
    {
        public override int Priority => GesturePriority.Zoom;

        public Int2 WheelDelta { get; private set; }
        public IReadOnlySet<FuKey> CurrentModifiers { get; private set; } =
            ImmutableHashSet<FuKey>.Empty;

        private readonly Action<MouseWheelGesture>? _onStart;
        private readonly Action<MouseWheelGesture>? _onUpdate;
        private readonly Action<MouseWheelGesture>? _onFinish;

        public MouseWheelGesture(
            IFuBehaviour behaviour,
            Action<MouseWheelGesture>? onStart = null,
            Action<MouseWheelGesture>? onUpdate = null,
            Action<MouseWheelGesture>? onFinish = null
        )
            : base(behaviour)
        {
            _onStart = onStart;
            _onUpdate = onUpdate;
            _onFinish = onFinish;
        }

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
                {
                    Status = GestureStatus.Finish;
                    _onFinish?.Invoke(this);
                }
                else
                    Status = GestureStatus.Idle;
                return;
            }

            // 2. Check Wheel Delta
            // We monitor Y (Vertical) wheel for standard zoom
            bool hasDelta = inputState.Mouse.WheelDelta.Y != 0;

            if (hasDelta)
            {
                // Capture state for the callback
                WheelDelta = inputState.Mouse.WheelDelta;
                CurrentModifiers = inputState.Modifiers;

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
                // No movement this frame
                if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                {
                    Status = GestureStatus.Finish;
                    _onFinish?.Invoke(this);
                }
                else
                    Status = GestureStatus.Idle;
            }
        }
    }
}
