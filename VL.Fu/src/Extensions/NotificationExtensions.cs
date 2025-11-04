using Stride.Core.Mathematics;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Extensions
{
    /// <summary>
    /// Provides extension methods for INotification and related types.
    /// </summary>
    public static class NotificationExtensions
    {
        public static Vector2 ToCommonSpace(
            this INotification notfication,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        )
        {
            if (notfication is NotificationWithPosition np)
            {
                var clientArea = np.ClientArea;
                return space switch
                {
                    CommonSpace.Normalized => np.Position.ToNormalizedSpace(clientArea),
                    CommonSpace.DIP => np.Position.ToCenteredDIPSpace(clientArea, dipFactor),
                    CommonSpace.DIPTopLeft => np.Position.ToDIPTopLeftSpace(dipFactor),
                    CommonSpace.PixelTopLeft => np.Position.ToPixelTopLeftSpace(pixelFactor),
                    _ => throw new InvalidOperationException("Space not found"),
                };
            }

            throw new InvalidOperationException("Notification does not have position information.");
        }

        /// <summary>
        /// Converts a raw INotification into a structured FuPointer, if applicable.
        /// </summary>
        /// <param name="notification">The source notification.</param>
        /// <param name="space">The coordinate space to transform the position into.</param>
        /// <param name="dipFactor">The DPI factor for coordinate transformation.</param>
        /// <param name="pixelFactor">The pixel scaling factor for coordinate transformation.</param>
        /// <returns>A FuPointer representing the input event, or FuPointer.Default if the notification is not a pointer event.</returns>
        public static FuPointer ToFuPointer(
            this INotification notification,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        )
        {
            return notification switch
            {
                MouseNotification mn => ToFuPointer(mn, space, dipFactor, pixelFactor),
                TouchNotification tn => ToFuPointer(tn, space, dipFactor, pixelFactor),
                _ => FuPointer.Default,
            };
        }

        private static FuPointer ToFuPointer(
            MouseNotification n,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        )
        {
            var position = n.ToCommonSpace(space, dipFactor, pixelFactor);
            var state = n switch
            {
                MouseDownNotification => TouchNotificationKind.TouchDown,
                MouseUpNotification => TouchNotificationKind.TouchUp,
                _ => TouchNotificationKind.TouchMove,
            };
            return new FuPointer
            {
                Id = Constants.MousePointerId,
                Position = position,
                State = state,
                TimeStamp = DateTimeOffset.UtcNow,
            };
        }

        private static FuPointer ToFuPointer(
            TouchNotification n,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        )
        {
            var position = n.ToCommonSpace(space, dipFactor, pixelFactor);
            return new FuPointer
            {
                Id = n.Id,
                Position = position,
                State = n.Kind,
                TimeStamp = DateTimeOffset.UtcNow,
            };
        }
    }
}
