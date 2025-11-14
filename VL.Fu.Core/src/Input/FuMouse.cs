using Stride.Core.Mathematics;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Input
{
    public record struct FuMouse
    {
        /// <summary>
        /// The position of the mouse in the common space.
        /// </summary>
        public Vector2 Position { get; init; }

        /// <summary>
        /// The accumulated mouse wheel value.
        /// X corresponds to the horizontal wheel, Y to the vertical wheel.
        /// </summary>
        public Int2 Wheel { get; init; }

        /// <summary>
        /// The change in the mouse wheel since the last event.
        /// This value is only non-zero for the duration of a wheel event.
        /// X corresponds to the horizontal wheel, Y to the vertical wheel.
        /// </summary>
        public Int2 WheelDelta { get; init; }

        /// <summary>
        /// The current state of the left mouse button.
        /// </summary>
        public bool IsLeft { get; init; }

        /// <summary>
        /// The current state of the right mouse button.
        /// </summary>
        public bool IsRight { get; init; }

        /// <summary>
        /// The current state of the middle mouse button.
        /// </summary>
        public bool IsMiddle { get; init; }

        /// <summary>
        /// The current state of the first extra mouse button.
        /// </summary>
        public bool IsXButton1 { get; init; }

        /// <summary>
        /// The current state of the second extra mouse button.
        /// </summary>
        public bool IsXButton2 { get; init; }

        /// <summary>
        /// The last button that was pressed or released.
        /// </summary>
        public MouseButtons Buttons { get; init; }

        /// <summary>
        /// The state of the mouse action (Down, Up, Move).
        /// </summary>
        public MouseNotificationKind State { get; init; }

        public FuMouse()
        {
            Position = Vector2.Zero;
            Wheel = Int2.Zero;
            WheelDelta = Int2.Zero;
            IsLeft = false;
            IsRight = false;
            IsMiddle = false;
            IsXButton1 = false;
            IsXButton2 = false;
            Buttons = MouseButtons.None;
            State = MouseNotificationKind.MouseMove;
        }
    }
}
