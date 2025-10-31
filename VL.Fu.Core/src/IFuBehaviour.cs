namespace VL.Fu.Core
{
    public interface IFuBehaviour
    {
        int Priority { get; }
        bool IsEnabled { get; }

        bool TryActivate(
            IFuNode node,
            IEnumerable<FuCursor> cursors,
            IEnumerable<FuKey> keys,
            IEnumerable<FuKey> modifiers
        );

        FuNodeState Evaluate(
            IFuNode node,
            IEnumerable<FuCursor> cursors,
            IEnumerable<FuKey> keys,
            IEnumerable<FuKey> modifiers
        );
    }
}
