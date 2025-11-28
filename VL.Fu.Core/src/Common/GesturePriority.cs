namespace VL.Fu.Core.Common
{
    /// <summary>
    /// Defines a set of standard priority levels for interactive behaviors.
    /// Higher numbers have higher priority and are processed first. This allows
    /// for clear arbitration when multiple behaviors could respond to the same input.
    /// </summary>
    public static class GesturePriority
    {
        public const int Highest = 1000;
        public const int Zoom = 900;
        public const int Drag = 800;
        public const int MarqueeSelection = 700;
        public const int Click = 500;
        public const int Hover = 200;
        public const int None = 0;
    }
}
