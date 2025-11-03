using VL.Core.Import;
using VL.Fu.Core;
using VL.Lib.Reactive;

namespace VL.Fu.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class HoverBehaviour : FuBehaviourBase, IFuBehaviour
    {
        protected readonly IChannel<bool> _isHoveredChannel =
            ChannelHelpers.CreateChannelOfType<bool>();

        [Fragment]
        public HoverBehaviour()
        {
            _isHoveredChannel.Value = false;
        }

        [Fragment]
        public bool IsHovered => _isHoveredChannel.Value;

        public override bool TryActivate(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        ) => _enabled.Value && cursors.Any(c => node.HitTest(c));

        public override bool TryAdvance(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        )
        {
            var isHovered = cursors.Any(c => node.HitTest(c));
            _isHoveredChannel.EnsureValue(isHovered);

            return isHovered;
        }
    }
}
