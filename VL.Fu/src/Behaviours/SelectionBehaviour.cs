using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Selections;
using VL.Fu.Extensions;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class SelectionBehaviour : FuBehaviourBase, IFuBehaviour
    {
        private ISelectionMode _mode;
        private int _activatingCursorId = -1;
        private bool _shouldClearOnStart = true;

        private RectangleF _committedSelectionRectangle;
        private bool _isCurrentlyActive;

        [Fragment]
        public SelectionBehaviour()
        {
            _priority.TrySetValue(Common.BehaviourPriority.Low);
        }

        [Fragment]
        public void SetMode(ISelectionMode mode)
        {
            if (_mode != mode)
            {
                _mode?.Reset();
                _mode = mode;
                _committedSelectionRectangle = RectangleF.Empty;
                _isCurrentlyActive = false;
            }
        }

        [Fragment]
        public RectangleF SelectionRectangle => _committedSelectionRectangle;

        [Fragment]
        public bool IsActive => _isCurrentlyActive;

        public override bool TryActivate(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        )
        {
            if (!IsEnabled || _mode is null)
                return false;

            var activatingCursor = cursors.FirstOrDefault(c =>
                c.State == TouchNotificationKind.TouchDown && node.HitTest(c)
            );
            if (activatingCursor.State != TouchNotificationKind.TouchDown)
                return false;

            var hitDescendant = node.TraversePreOrder()
                .OfType<IFuNode>()
                .Skip(1)
                .FirstOrDefault(child => child.HitTest(activatingCursor));

            if (hitDescendant != null)
            {
                return false;
            }

            _activatingCursorId = activatingCursor.Id;
            _mode.Start(activatingCursor.Position, modifiers);
            _isCurrentlyActive = true;

            if (_shouldClearOnStart)
            {
                UpdateSelectionState(node, RectangleF.Empty, false);
            }

            return true;
        }

        public override bool TryAdvance(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        )
        {
            var advancingCursor = cursors.FirstOrDefault(c => c.Id == _activatingCursorId);

            if (
                advancingCursor.Id != _activatingCursorId
                || advancingCursor.State == TouchNotificationKind.TouchUp
            )
            {
                _mode?.End();

                // --- THIS IS THE FIX ---
                // We must also reset the mode to discard its internal state (like the start position).
                _mode?.Reset();
                // -----------------------

                _isCurrentlyActive = false;
                return false;
            }

            _mode.UpdateSelection(advancingCursor.Position, modifiers);
            _committedSelectionRectangle = _mode.SelectionRectangle;

            UpdateSelectionState(node, _committedSelectionRectangle, _isCurrentlyActive);
            return true;
        }

        private void UpdateSelectionState(
            IFuNode hostNode,
            RectangleF selectionArea,
            bool isSelectionActive
        )
        {
            foreach (var childNode in hostNode.TraversePostOrder().OfType<IFuNode>())
            {
                var selectableBehaviour = childNode
                    .Behaviours.OfType<SelectableBehaviour>()
                    .FirstOrDefault();

                if (selectableBehaviour != null)
                {
                    bool isSelected = isSelectionActive && childNode.IsContainedIn(selectionArea);
                    selectableBehaviour.SetSelected(isSelected);
                }
            }
        }
    }
}
