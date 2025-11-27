namespace VL.Fu.Core.Interaction
{
    public enum GesturePhase
    {
        Idle,

        /// <summary>
        /// The gesture is tracking input but hasn't committed yet.
        /// (e.g. MouseDown for a Click, or Drag before threshold).
        /// Conflicts are tolerated in this phase.
        /// </summary>
        Possible,

        /// <summary>
        /// The gesture has recognized its pattern and is committing.
        /// (e.g. Drag threshold crossed).
        /// Triggers cancellation of conflicting 'Possible' gestures.
        /// </summary>
        Began,

        /// <summary>
        /// Continuous updates after Began.
        /// </summary>
        Changed,

        /// <summary>
        /// Successful completion (e.g. MouseUp).
        /// </summary>
        Matched,

        Failed,
        Cancelled,
    }

    public readonly record struct FuGestureState(GesturePhase Phase, FuGestureEvent? Event = null)
    {
        public static readonly FuGestureState Idle = new(GesturePhase.Idle);

        public bool IsTerminal =>
            Phase == GesturePhase.Matched
            || Phase == GesturePhase.Failed
            || Phase == GesturePhase.Cancelled;

        public bool IsSuccess => Phase == GesturePhase.Matched;

        // Active means it belongs in the session list
        public bool IsActive =>
            Phase == GesturePhase.Possible
            || Phase == GesturePhase.Began
            || Phase == GesturePhase.Changed
            || Phase == GesturePhase.Matched; // Matched is technically active until processed

        // Fluent Factories
        public static FuGestureState Possible(FuGestureEvent ev) => new(GesturePhase.Possible, ev);

        public static FuGestureState Began(FuGestureEvent ev) => new(GesturePhase.Began, ev);

        public static FuGestureState Changed(FuGestureEvent ev) => new(GesturePhase.Changed, ev);

        public static FuGestureState Matched(FuGestureEvent ev) => new(GesturePhase.Matched, ev);

        public static FuGestureState Fail(FuGestureEvent? ev = null) =>
            new(GesturePhase.Failed, ev);

        public static FuGestureState Cancel() => new(GesturePhase.Cancelled, null);
    }
}
