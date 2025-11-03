using VL.Core.Import;
using VL.Fu.Core;
using VL.Lib.Reactive;

namespace VL.Fu.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class SelectableBehaviour : FuBehaviourBase
    {
        public readonly IChannel<bool> IsSelectedChannel =
            ChannelHelpers.CreateChannelOfType<bool>();

        [Fragment]
        public SelectableBehaviour()
        {
            // This behaviour doesn't need to interact, so we can disable it.
            _enabled.TrySetValue(false);
            IsSelectedChannel.Value = false;
        }

        [Fragment]
        public bool IsSelected => IsSelectedChannel.Value;

        /// <summary>
        /// Input to externally set the selection state of this item.
        /// </summary>
        [Fragment]
        public void SetSelected(bool value)
        {
            IsSelectedChannel.EnsureValue(value);
        }

        public override bool TryActivate(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        )
        {
            return false;
        }

        public override bool TryAdvance(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        )
        {
            return false;
        }
    }
}
