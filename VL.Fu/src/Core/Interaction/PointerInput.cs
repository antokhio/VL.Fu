using Stride.Core.Mathematics;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Decodes the pointer notifications a Fu node hands to its behaviors into <see cref="PointerSample"/>s,
    /// with one switch shared by every behavior.
    /// </summary>
    internal static class PointerInput
    {
        /// <summary>
        /// Raw wheel units per notch, as Windows reports them.
        /// </summary>
        public const float WheelUnitsPerNotch = 120f;

        /// <summary>
        /// Reads <paramref name="notification"/> as a press, move, release or wheel turn of the mouse or a touch.
        /// False for every other notification, and for one whose sender isn't a <see cref="FuNotificationSender"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="MouseClickNotification"/> is not read: Fu recognizes clicks itself from Down and Up.
        /// Gesture notifications, device loss, keys and Fu's own pointer notifications aren't samples either;
        /// behaviors switch on those directly.
        /// </remarks>
        public static bool TryRead(INotification notification, out PointerSample sample)
        {
            if (notification.Sender is not FuNotificationSender sender)
            {
                sample = default;
                return false;
            }

            switch (notification)
            {
                case MouseDownNotification down:
                    sample = Create(sender, down, PointerSampleKind.Down, PointerIds.Mouse, down.Buttons, Vector2.Zero);
                    return true;
                case MouseUpNotification up:
                    sample = Create(sender, up, PointerSampleKind.Up, PointerIds.Mouse, up.Buttons, Vector2.Zero);
                    return true;
                case MouseMoveNotification move:
                    sample = Create(sender, move, PointerSampleKind.Move, PointerIds.Mouse, MouseButtons.None, Vector2.Zero);
                    return true;
                case MouseWheelNotification wheel:
                    var notchesY = wheel.WheelDelta / WheelUnitsPerNotch;
                    sample = Create(sender, wheel, PointerSampleKind.Wheel, PointerIds.Mouse, MouseButtons.None, new Vector2(0f, notchesY));
                    return true;
                case MouseHorizontalWheelNotification horizontalWheel:
                    var notchesX = horizontalWheel.WheelDelta / WheelUnitsPerNotch;
                    sample = Create(sender, horizontalWheel, PointerSampleKind.Wheel, PointerIds.Mouse, MouseButtons.None, new Vector2(notchesX, 0f));
                    return true;
                case TouchNotification touch:
                    var kind = touch.Kind switch
                    {
                        TouchNotificationKind.TouchDown => PointerSampleKind.Down,
                        TouchNotificationKind.TouchUp => PointerSampleKind.Up,
                        _ => PointerSampleKind.Move,
                    };
                    var button = kind == PointerSampleKind.Move ? MouseButtons.None : MouseButtons.Left;
                    sample = Create(sender, touch, kind, touch.Id, button, Vector2.Zero);
                    return true;
                default:
                    sample = default;
                    return false;
            }
        }

        private static PointerSample Create(
            FuNotificationSender sender,
            NotificationWithPosition notification,
            PointerSampleKind kind,
            int pointerId,
            MouseButtons button,
            Vector2 wheelDelta
        ) =>
            new()
            {
                Kind = kind,
                PointerId = pointerId,
                Button = button,
                Modifiers = notification.ModifierKeys,
                PositionInWindow = notification.Position,
                WheelDelta = wheelDelta,
                Node = sender.Node,
                WindowToNode = sender.WindowToNodeMatrix,
                WindowToParent = sender.WindowToParentMatrix,
                Scaling = sender.Scaling,
                TimeMs = sender.TimeMs,
            };
    }
}
