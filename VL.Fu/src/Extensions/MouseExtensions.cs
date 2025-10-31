using VL.Fu.Core;
using VL.Fu.Helpers;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Skia;
using FuMouse = VL.Fu.Core.FuMouse;

namespace VL.Fu.Extensions
{
    public static class MouseExtensions
    {
        public static FuMouse With(
            this FuMouse mouse,
            MouseDownNotification notification,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        ) =>
            notification.Buttons switch
            {
                MouseButtons.Left => mouse with
                {
                    IsLeftButton = true,
                    Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
                },
                MouseButtons.Right => mouse with
                {
                    IsRightButton = true,
                    Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
                },
                MouseButtons.Middle => mouse with
                {
                    IsMiddleButton = true,
                    Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
                },
                MouseButtons.XButton1 => mouse with
                {
                    IsXButton1 = true,
                    Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
                },
                MouseButtons.XButton2 => mouse with
                {
                    IsXButton2 = true,
                    Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
                },
                _ => mouse,
            };

        public static FuMouse With(
            this FuMouse mouse,
            MouseUpNotification notification,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        ) =>
            notification.Buttons switch
            {
                MouseButtons.Left => mouse with
                {
                    IsLeftButton = false,
                    Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
                },
                MouseButtons.Right => mouse with
                {
                    IsRightButton = false,
                    Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
                },
                MouseButtons.Middle => mouse with
                {
                    IsMiddleButton = false,
                    Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
                },
                MouseButtons.XButton1 => mouse with
                {
                    IsXButton1 = false,
                    Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
                },
                MouseButtons.XButton2 => mouse with
                {
                    IsXButton2 = false,
                    Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
                },
                _ => mouse,
            };

        public static FuMouse With(
            this FuMouse mouse,
            MouseMoveNotification notification,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        ) =>
            mouse with
            {
                Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),

                IsLost = notification.IsMouseLost(),
            };

        public static FuMouse With(
            this FuMouse mouse,
            MouseWheelNotification notification,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        ) =>
            mouse with
            {
                Position = notification.ToCommonSpace(space, dipFactor, pixelFactor),
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
