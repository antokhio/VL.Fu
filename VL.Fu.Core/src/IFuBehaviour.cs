namespace VL.Fu.Core
{
    public interface IFuBehaviour
    {
        int Priority { get; }
        bool IsEnabled { get; }
        bool TryActivate(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        );
        bool TryAdvance(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        );
    }
}
