using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.HitTest;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Behaviours
{
    [ProcessNode(Name = "Click (Behaviour)", FragmentSelection = FragmentSelection.Explicit)]
    public class ClickBehaviour : FuBehaviourBase, IFuBehaviour
    {
        public int Priority => 20; // Higher priority than hover
        public bool IsEnabled => true;

        // Internal state tracking for click eligibility
        private bool _cursorWasPressedOnThisNode = false;

        [Fragment]
        public ClickBehaviour() { }

        // We don't implement TryActivate for a click behaviour here, the service just runs Evaluate always
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
            // Use Identity() to start clean, only setting the properties this behavior owns
            var stateDelta = FuNodeState.Identity();

            bool clickOccurredThisFrame = false;

            if (node is IHitTestProvider hitProvider)
            {
                foreach (var cursor in cursors)
                {
                    if (hitProvider.HitTest(cursor)) // Check if cursor is over this node
                    {
                        if (cursor.State == TouchNotificationKind.TouchDown)
                        {
                            // If pressed over this node, mark it as eligible for a click
                            _cursorWasPressedOnThisNode = true;
                        }
                        else if (cursor.State == TouchNotificationKind.TouchUp)
                        {
                            // If released over this node AND it was pressed here originally
                            if (_cursorWasPressedOnThisNode)
                            {
                                clickOccurredThisFrame = true;
                            }
                            _cursorWasPressedOnThisNode = false; // Reset state after release
                        }
                    }
                    else if (cursor.State == TouchNotificationKind.TouchUp)
                    {
                        // If released OFF of the node, cancel eligibility
                        _cursorWasPressedOnThisNode = false;
                    }
                }
            }

            stateDelta.OnClick = clickOccurredThisFrame;
            // Click behavior also likely sets IsSelected in many UIs, but we stick to just the click event for now.

            return stateDelta;
        }
    }
}
