using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Interaction.Gestures
{
    public class PointerOverGesture : GestureBase
    {
        public override int Priority => GesturePriority.Hover;

        public PointerOverGesture(IFuBehaviour behaviour)
            : base(behaviour) { }

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
                // If we have pointers, we are active
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
                // No pointers -> Finish if we were active, otherwise Idle
                if (Status == GestureStatus.Start || Status == GestureStatus.Update)
                    Status = GestureStatus.Finish;
                else
                    Status = GestureStatus.Idle;
            }
        }
    }
}
