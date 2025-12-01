using Stride.Core.Mathematics;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Interaction.Gestures
{
    public class PointerOverGesture : GestureBase
    {
        public override int Priority => GesturePriority.Hover;

        public Vector2 CurrentPosition { get; private set; }

        private readonly Action<PointerOverGesture>? _onStart;
        private readonly Action<PointerOverGesture>? _onUpdate;
        private readonly Action<PointerOverGesture>? _onFinish;

        public PointerOverGesture(
            IFuBehaviour behaviour,
            Action<PointerOverGesture>? onStart = null,
            Action<PointerOverGesture>? onUpdate = null,
            Action<PointerOverGesture>? onFinish = null
        )
            : base(behaviour)
        {
            _onStart = onStart;
            _onUpdate = onUpdate;
            _onFinish = onFinish;
        }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            // For PointerOver, "candidates" contains exactly what we need:
            // The list of pointers that are currently hitting the Host node.
            // We simply sync our state to match this list.

            _activators.Clear();
            foreach (var p in candidates)
            {
                _activators.Add(p);
            }

            if (_activators.Count > 0)
            {
                CurrentPosition = _activators[0].Position;

                // If we have pointers, we are active
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
                // No pointers -> Finish if we were active, otherwise Idle
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
    }
}
