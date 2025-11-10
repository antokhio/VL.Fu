using VL.Fu.Core.Gesture;

namespace VL.Fu.Gestures
{
    /// <summary>
    /// A gesture that detects when a pointer enters or exits the bounds of a host.
    /// It matches on entry and reports exit through its status.
    /// </summary>
    public class HoverGesture : GestureBase
    {
        // Internal state to track if the pointer is currently inside the host's bounds.
        private bool _isCurrentlyInside;

        protected override void OnProcessInput(GestureInputContext context)
        {
            var pointerIsInside = Host?.HitTest(context.PrimaryPointer) ?? false;

            if (pointerIsInside && !_isCurrentlyInside)
            {
                // --- Pointer has entered ---
                _isCurrentlyInside = true;
                Status = GestureStatus.Matched; // Activate on entry
                ActivationData = context.PrimaryPointer;
            }
            else if (!pointerIsInside && _isCurrentlyInside)
            {
                // --- Pointer has exited ---
                _isCurrentlyInside = false;
                Status = GestureStatus.Failed; // Use Failed to signal exit to the behavior
            }
        }

        public override void Reset()
        {
            // Do not reset _isCurrentlyInside, as it needs to persist across pointer events.
            Status = GestureStatus.Ready;
            ActivationData = null;
        }

        /// <summary>
        /// Public method to force the hover state to false. Used by the behavior on deactivation.
        /// </summary>
        public void ForceExit()
        {
            _isCurrentlyInside = false;
            Status = GestureStatus.Ready;
        }
    }
}
