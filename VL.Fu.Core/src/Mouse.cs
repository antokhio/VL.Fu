using System.Numerics;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core
{
    public record struct Mouse
    {
        public Vector2 Position { get; set; } = Vector2.Zero;
        public int Wheel { get; set; } = 0;
        public bool IsLeftButton { get; set; } = false;
        public bool IsRightButton { get; set; } = false;
        public bool IsMiddleButton { get; set; } = false;
        public bool IsXButton1 { get; set; } = false;
        public bool IsXButton2 { get; set; } = false;
        public bool IsLost { get; set; } = false;

        public TouchNotificationKind CursorState { get; set; } = TouchNotificationKind.TouchUp;

        public Mouse() { }
    }
}
