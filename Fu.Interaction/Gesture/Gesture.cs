namespace Fu.Interaction.Gesture
{
    public enum GestureState
    {
        /// <summary>
        /// The gesture has been recognized and started.
        /// </summary>
        Started,

        /// <summary>
        /// The gesture parameters have changed (e.g., pointer moved during a pan).
        /// </summary>
        Updated,

        /// <summary>
        /// The gesture finished successfully (e.g., pointer released).
        /// </summary>
        Completed,

        /// <summary>
        /// The gesture was interrupted or rejected by the system/behavior.
        /// </summary>
        Canceled,
    }

    public abstract record class Gesture
    {
        public GestureState State { get; init; } = GestureState.Started;
    }

    public static class GestureExtensions
    {
        public static bool IsActive(this Gesture gesture) =>
            gesture.State is GestureState.Started or GestureState.Updated;

        public static bool IsIdle(this Gesture gesture) =>
            gesture.State is GestureState.Completed or GestureState.Canceled;
    }
}
