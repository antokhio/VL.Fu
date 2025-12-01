using VL.Fu.Core.Input;

namespace VL.Fu.Core.Gesture
{
    /// <summary>
    /// Provides all necessary input state for a gesture to process.
    /// </summary>
    public record GestureInputContext(
        FuPointer PrimaryPointer,
        IReadOnlyDictionary<int, FuPointer> AllPointers,
        IReadOnlySet<FuKey> KeysDown,
        DateTimeOffset Timestamp
    );
}
