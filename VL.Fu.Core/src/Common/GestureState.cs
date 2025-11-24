namespace VL.Fu.Core.Common
{
    /// <summary>
    /// Represents the state of a gesture's recognition lifecycle.
    /// </summary>
    public enum GestureState
    {
        Ready,
        Possible,
        Matched,
        Failed,
        Cancelled,
    }
}
