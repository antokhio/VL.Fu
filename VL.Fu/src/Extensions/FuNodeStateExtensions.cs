using VL.Fu.Core;

namespace VL.Fu.Extensions
{
    public static class FuNodeStateExtensions
    {
        public static FuNodeState Identity(this FuNodeState state) =>
            state with
            {
                OnClick = false,
                IsHover = false,
                OnHoverStart = false,
                OnHoverEnd = false,
                IsDrag = false,
                OnDragStart = false,
                OnDragEnd = false,
                IsSelected = false,
                OnSelected = false,
                OnDeselected = false,
            };

        public static FuNodeState OptimisticMerge(this FuNodeState current, FuNodeState next) =>
            current with
            {
                OnClick = next.OnClick || current.OnClick,
                IsHover = next.IsHover || current.IsHover,
                OnHoverStart = next.OnHoverStart || current.OnHoverStart,
                OnHoverEnd = next.OnHoverEnd || current.OnHoverEnd,
                IsDrag = next.IsDrag || current.IsDrag,
                OnDragStart = next.OnDragStart || current.OnDragStart,
                OnDragEnd = next.OnDragEnd || current.OnDragEnd,
                IsSelected = next.IsSelected || current.IsSelected,
                OnSelected = next.OnSelected || current.OnSelected,
                OnDeselected = next.OnDeselected || current.OnDeselected,
            };
    }
}
