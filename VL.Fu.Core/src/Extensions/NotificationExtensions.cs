using System.Numerics;
using VL.Fu.Core.Input;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core.Extensions
{
    public static class NotificationExtensions
    {
        public static FuPointer ToFuPointer(
            this TouchNotification notification,
            FuViewport viewport
        )
        {
            if (notification is NotificationWithPosition nwp)
            {
                var position = nwp.ToCurrentSpace(viewport);
                return new FuPointer(notification.Id, position, notification.Kind);
            }

            throw new ArgumentOutOfRangeException(nameof(notification));
        }

        public static Vector2 ToCurrentSpace(
            this NotificationWithPosition notification,
            FuViewport viewport
        )
        {
            return viewport.Space switch
            {
                CommonSpace.Normalized => notification.Position.ToNormalizedSpace(
                    notification.ClientArea
                ),
                CommonSpace.DIP => notification.Position.ToCenteredDIPSpace(
                    notification.ClientArea,
                    viewport.DIPFactor
                ),
                CommonSpace.DIPTopLeft => notification.Position.ToDIPTopLeftSpace(
                    viewport.DIPFactor
                ),
                CommonSpace.PixelTopLeft => notification.Position.ToPixelTopLeftSpace(
                    viewport.PixelFactor
                ),
                _ => throw new ArgumentOutOfRangeException(nameof(viewport.Space)),
            };
        }
    }
}
