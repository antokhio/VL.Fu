using VL.Core.Import;
using VL.Fu.Behaviours;
using VL.Fu.Core.HitTest;

namespace VL.Fu.Core.Behaviour
{
    [ProcessNode(Name = "Hover (Behaviour)", FragmentSelection = FragmentSelection.Explicit)]
    public class HoverBehaviour : FuBehaviourBase, IFuBehaviour
    {
        private bool _wasHoveredLastFrame = false;

        [Fragment]
        public HoverBehaviour() { }

        public override bool TryActivate(
            IFuNode node,
            IEnumerable<FuCursor> cursors,
            IEnumerable<FuKey> keys,
            IEnumerable<FuKey> modifiers
        ) => true;

        public override FuNodeState Evaluate(
            IFuNode node,
            IEnumerable<FuCursor> cursors,
            IEnumerable<FuKey> keys,
            IEnumerable<FuKey> modifiers
        )
        {
            bool isCurrentlyHovered = false;

            if (node is IHitTestProvider hitProvider)
            {
                isCurrentlyHovered = cursors.Any(cursor => hitProvider.HitTest(cursor));
            }

            bool onHoverStart = false;
            bool onHoverEnd = false;

            if (isCurrentlyHovered && !_wasHoveredLastFrame)
            {
                onHoverStart = true;
            }
            else if (!isCurrentlyHovered && _wasHoveredLastFrame)
            {
                onHoverEnd = true;
            }

            _wasHoveredLastFrame = isCurrentlyHovered;

            return node.State with
            {
                IsHover = isCurrentlyHovered,
                OnHoverStart = onHoverStart,
                OnHoverEnd = onHoverEnd,
            };
        }
    }
}
