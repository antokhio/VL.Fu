namespace VL.Fu.Core.Gesture
{
    /// <summary>
    /// Represents the state of a gesture's recognition lifecycle.
    /// </summary>
    public enum GestureStatus
    {
        Ready,
        Possible,
        Matched,
        Failed,
        Cancelled,
    }
}
