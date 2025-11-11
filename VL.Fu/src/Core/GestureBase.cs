using VL.Core.Import;

namespace VL.Fu.Core.Gesture
{
    /// <summary>
    /// An abstract base class for all gesture recognizers.
    /// It provides the common state management logic for the IGesture interface.
    /// </summary>
    [ProcessNode]
    public abstract class GestureBase : RepositoryConsumer, IGesture
    {
        /// <summary>
        /// The host that this gesture is scoped to.
        /// </summary>
        protected IInteractiveHost? Host { get; private set; }

        /// <summary>
        /// The current state of the gesture's recognition process.
        /// The setter is protected to ensure state is managed by the gesture's own logic.
        /// </summary>
        public GestureStatus Status { get; protected set; } = GestureStatus.Ready;

        /// <summary>
        /// The contextual data available when the gesture's status becomes 'Matched'.
        /// The setter is protected, intended to be set by the derived class upon a successful match.
        /// </summary>
        public object? ActivationData { get; protected set; }

        public void SetHost(IInteractiveHost? host)
        {
            Host = host;
        }

        /// <summary>
        /// Processes the input and updates the gesture's status.
        /// The base implementation provides a guard to stop processing once the gesture
        /// has reached a terminal state (Matched, Failed, or Cancelled).
        /// </summary>
        /// <param name="context">The current input context.</param>
        public void ProcessInput(GestureInputContext context)
        {
            // Do not process further if the gesture has already concluded.
            if (
                Status == GestureStatus.Matched
                || Status == GestureStatus.Failed
                || Status == GestureStatus.Cancelled
            )
            {
                return;
            }

            OnProcessInput(context);
        }

        /// <summary>
        /// Derived classes must implement this method to define their specific recognition logic.
        /// This method is called by the public ProcessInput after the guard condition has been checked.
        /// </summary>
        protected abstract void OnProcessInput(GestureInputContext context);

        /// <summary>
        /// Resets the gesture to its initial 'Ready' state, clearing any activation data.
        /// Can be overridden by derived classes to reset more specific internal state.
        /// </summary>
        public virtual void Reset()
        {
            Status = GestureStatus.Ready;
            ActivationData = null;
        }

        /// <summary>
        /// Forcibly moves the gesture to the 'Cancelled' state.
        /// </summary>
        public virtual void Cancel()
        {
            Status = GestureStatus.Cancelled;
        }
    }
}
