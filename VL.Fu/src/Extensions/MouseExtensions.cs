using VL.Fu.Core;
using VL.Fu.Helpers;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using FuMouse = VL.Fu.Core.FuMouse;

namespace VL.Fu.Extensions
{
    public static class MouseExtensions
    {
        public static FuMouse With(this FuMouse mouse, MouseDownNotification notification) =>
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

        public static FuMouse With(this FuMouse mouse, MouseUpNotification notification) =>
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

        public static FuMouse With(this FuMouse mouse, MouseMoveNotification notification) =>
            mouse with
            {
                Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                IsLost = notification.IsMouseLost(),
            };

        public static FuMouse With(this FuMouse mouse, MouseWheelNotification notification) =>
            mouse with
            {
                Position = notification.PositionInProjectionSpace.ToSkiaSpace(),
                Wheel = mouse.Wheel + notification.WheelDelta,
                IsLost = notification.IsMouseLost(),
            };

        public static FuMouse With(this FuMouse mouse, MouseLostNotification notification) =>
            mouse with
            {
                IsLost = notification.IsMouseLost(),
            };

        public static FuCursor ToNewFuCursor(this FuMouse mouse, TouchNotificationKind state) =>
            new FuCursor(MouseHelper.MouseCursorId, mouse.Position, state);

        public static FuCursor ToFuCursorWithDelta(
            this FuMouse mouse,
            FuCursor previous,
            TouchNotificationKind state
        )
        {
            var delta = previous.Position - mouse.Position;
            var distance = previous.Distance + delta;

            return previous with
            {
                Position = mouse.Position,
                Delta = previous.Position - mouse.Position,
                Distance = distance,
            };
        }
    }
}
