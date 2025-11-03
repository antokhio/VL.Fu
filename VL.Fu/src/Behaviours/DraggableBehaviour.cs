using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Helpers;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;

namespace VL.Fu.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class DraggableBehaviour : FuBehaviourBase, IFuBehaviour
    {
        public IChannel<Vector2> OffsetChannel { get; } =
            ChannelHelpers.CreateChannelOfType<Vector2>();
        public IChannel<bool> IsDraggingChannel { get; } =
            ChannelHelpers.CreateChannelOfType<bool>();

        private bool _isDragging;
        private int _draggingCursorId = -1;
        private Vector2 _dragStartCursorPos;
        protected Vector2 _committedOffset = Vector2.Zero;

        // This property is ONLY for clamping the drag offset. It has no effect on hit-testing.
        protected readonly CachedProperty<RectangleF?> _clampingBounds = new(null);

        [Fragment]
        public DraggableBehaviour()
        {
            _priority.TrySetValue(Common.BehaviourPriority.Drag);
            IsDraggingChannel.Value = false;
            OffsetChannel.Value = Vector2.Zero;
        }

        /// <summary>
        /// Sets the rectangle that will constrain the drag offset. This does NOT affect hit-testing.
        /// </summary>
        [Fragment]
        public void SetClampingBounds(RectangleF? bounds) => _clampingBounds.TrySetValue(bounds);

        [Fragment]
        public Vector2 Offset => OffsetChannel.Value;

        [Fragment]
        public bool IsDragging => IsDraggingChannel.Value;

        public override bool TryActivate(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        )
        {
            if (_isDragging || !IsEnabled)
                return false;
            var activatingCursor = cursors.FirstOrDefault(c =>
                c.State == TouchNotificationKind.TouchDown && node.HitTest(c)
            );
            if (activatingCursor.State != TouchNotificationKind.TouchDown)
                return false;

            _isDragging = true;
            _draggingCursorId = activatingCursor.Id;
            _dragStartCursorPos = activatingCursor.Position;
            IsDraggingChannel.EnsureValue(true);
            return true;
        }

        public override bool TryAdvance(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        )
        {
            if (!_isDragging)
                return false;

            var advancingCursor = cursors.FirstOrDefault(c => c.Id == _draggingCursorId);
            if (advancingCursor.Id != _draggingCursorId)
            {
                _isDragging = false;
                _draggingCursorId = -1;
                IsDraggingChannel.EnsureValue(false);
                _committedOffset = OffsetChannel.Value;
                return false;
            }

            var currentDragDelta = advancingCursor.Position - _dragStartCursorPos;
            var newTotalOffset = CalculateOffset(currentDragDelta);

            OffsetChannel.EnsureValue(newTotalOffset);

            if (advancingCursor.State == TouchNotificationKind.TouchUp)
            {
                _isDragging = false;
                _draggingCursorId = -1;
                IsDraggingChannel.EnsureValue(false);
                _committedOffset = newTotalOffset;
                return false;
            }

            return true;
        }

        protected virtual Vector2 CalculateOffset(Vector2 currentDragDelta)
        {
            var newTotalOffset = _committedOffset + currentDragDelta;

            var bounds = _clampingBounds.Value;
            if (bounds.HasValue)
            {
                newTotalOffset.X = Math.Clamp(
                    newTotalOffset.X,
                    bounds.Value.Left,
                    bounds.Value.Right
                );
                newTotalOffset.Y = Math.Clamp(
                    newTotalOffset.Y,
                    bounds.Value.Top,
                    bounds.Value.Bottom
                );
            }
            return newTotalOffset;
        }
    }

    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class DraggableXBehaviour : DraggableBehaviour
    {
        [Fragment]
        public DraggableXBehaviour() { }

        protected override Vector2 CalculateOffset(Vector2 currentDragDelta)
        {
            // Calculate the full potential offset using the base class logic (including clamping)
            var baseOffset = base.CalculateOffset(currentDragDelta);

            // Constrain to X-axis by preserving the committed Y offset
            baseOffset.Y = _committedOffset.Y;

            return baseOffset;
        }
    }

    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class DraggableYBehaviour : DraggableBehaviour
    {
        [Fragment]
        public DraggableYBehaviour()
            : base() { }

        protected override Vector2 CalculateOffset(Vector2 currentDragDelta)
        {
            // Calculate the full potential offset using the base class logic (including clamping)
            var baseOffset = base.CalculateOffset(currentDragDelta);

            // Constrain to Y-axis by preserving the committed X offset
            baseOffset.X = _committedOffset.X;

            return baseOffset;
        }
    }
}
