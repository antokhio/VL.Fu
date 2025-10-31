namespace VL.Fu.Core
{
    public record struct FuNodeState
    {
        public bool IsHover { get; set; }
        public bool OnHoverStart { get; set; }
        public bool OnHoverEnd { get; set; }
        public bool OnClick { get; set; }
        public bool IsDrag { get; set; }
        public bool OnDragStart { get; set; }
        public bool OnDragEnd { get; set; }
        public bool IsSelected { get; set; }
        public bool OnSelected { get; set; }
        public bool OnDeselected { get; set; }
        public bool IsEnabled { get; set; }

        public static FuNodeState Identity() => new FuNodeState();
    }
}
