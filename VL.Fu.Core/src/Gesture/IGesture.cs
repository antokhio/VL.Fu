namespace VL.Fu.Core.Gesture
{
    /// <summary>
    /// Defines the contract for an object that recognizes a specific input pattern.
    /// </summary>
    public interface IGesture
    {
        /// <summary>
        /// The current state of the gesture's recognition process.
        /// </summary>
        GestureStatus Status { get; }

        /// <summary>
        /// The contextual data available when the gesture's status becomes 'Matched'.
        /// </summary>
        object? ActivationData { get; }

        /// <summary>
        /// Processes the current input context and updates the gesture's internal state.
        /// </summary>
        /// <param name="context">The state of all relevant inputs for the current frame.</param>
        void ProcessInput(GestureInputContext context);

        /// <summary>
        /// Resets the gesture back to its 'Ready' state. Called when an interaction sequence ends.
        /// </summary>
        void Reset();

        /// <summary>
        /// Forcibly moves the gesture to the 'Cancelled' state.
        /// </summary>
        void Cancel();
    }
}
