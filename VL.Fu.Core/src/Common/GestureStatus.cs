namespace VL.Fu.Core.Common
{
    public enum GestureStatus
    {
        /// <summary>
        /// The gesture is actively tracking input and hasn't decided the outcome yet.
        /// </summary>
        Running,

        /// <summary>
        /// The gesture successfully recognized the pattern (e.g. Clicked, Drag Ended).
        /// The interaction will end.
        /// </summary>
        Matched,

        /// <summary>
        /// The gesture failed to recognize the pattern (e.g. Released outside, Drag aborted).
        /// The interaction will end.
        /// </summary>
        Failed,
    }
}
