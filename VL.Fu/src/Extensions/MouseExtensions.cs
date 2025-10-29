using VL.Fu.Core;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using Mouse = VL.Fu.Core.Mouse;

namespace VL.Fu.Extensions
{
    public static class MouseExtensions
    {
        public static Mouse With(this Mouse mouse, MouseDownNotification notification) =>
            notification.Buttons switch
            {
                MouseButtons.Left => mouse with
                {
                    IsLeftButton = true,
                    Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                },
                MouseButtons.Right => mouse with
                {
                    IsRightButton = true,
                    Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                },
                MouseButtons.Middle => mouse with
                {
                    IsMiddleButton = true,
                    Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                },
                MouseButtons.XButton1 => mouse with
                {
                    IsXButton1 = true,
                    Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                },
                MouseButtons.XButton2 => mouse with
                {
                    IsXButton2 = true,
                    Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                },
                _ => mouse,
            };

        public static Mouse With(this Mouse mouse, MouseUpNotification notification) =>
            notification.Buttons switch
            {
                MouseButtons.Left => mouse with
                {
                    IsLeftButton = false,
                    Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                },
                MouseButtons.Right => mouse with
                {
                    IsRightButton = false,
                    Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                },
                MouseButtons.Middle => mouse with
                {
                    IsMiddleButton = false,
                    Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                },
                MouseButtons.XButton1 => mouse with
                {
                    IsXButton1 = false,
                    Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                },
                MouseButtons.XButton2 => mouse with
                {
                    IsXButton2 = false,
                    Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                },
                _ => mouse,
            };

        public static Mouse With(this Mouse mouse, MouseMoveNotification notification) =>
            mouse with
            {
                Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                IsLost = notification.IsMouseLost(),
            };

        public static Mouse With(this Mouse mouse, MouseWheelNotification notification) =>
            mouse with
            {
                Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                Wheel = mouse.Wheel + notification.WheelDelta,
                IsLost = notification.IsMouseLost(),
            };

        public static Mouse With(this Mouse mouse, MouseLostNotification notification) =>
            mouse with
            {
                IsLost = notification.IsMouseLost(),
            };

        public static Cursor ToCursor(this Mouse mouse, TouchNotificationKind state) =>
            new Cursor
            {
                Id = -1,
                Position = mouse.Position,
                State = state,
            };
    }
}
