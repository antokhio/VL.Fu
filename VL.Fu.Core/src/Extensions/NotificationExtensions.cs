using Stride.Core.Mathematics;
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
                CommonSpace.Normalized => notification.ToNormalizedSpace(),
                CommonSpace.DIP => notification.ToDIPCenteredSpace(viewport.DIPFactor),
                CommonSpace.DIPTopLeft => notification.ToDIPTopLeftSpace(viewport.DIPFactor),
                CommonSpace.PixelTopLeft => notification.ToPixelTopLeftSpace(viewport.PixelFactor),
                _ => throw new ArgumentOutOfRangeException(nameof(viewport.Space)),
            };
        }

        public static Vector2 ToNormalizedSpace(this NotificationWithPosition notification)
        {
            float aspect = notification.ClientArea.X / notification.ClientArea.Y;
            var norm = new Vector2(
                (notification.Position.X / notification.ClientArea.X) * 2f - 1f,
                (notification.Position.Y / notification.ClientArea.Y) * 2f - 1f
            );

            if (aspect > 1f)
                norm.X *= aspect;
            else
                norm.Y /= aspect;

            return norm;
        }

        public static Vector2 ToDIPCenteredSpace(
            this NotificationWithPosition notification,
            int dipFactor
        )
        {
            var halfSize = new Vector2(
                (notification.ClientArea.X / dipFactor) / 2f,
                (notification.ClientArea.Y / dipFactor) / 2f
            );

            var norm = new Vector2(
                (notification.Position.X / dipFactor) - halfSize.X,
                (notification.Position.Y / dipFactor) - halfSize.Y
            );

            return norm;
        }

        public static Vector2 ToDIPTopLeftSpace(
            this NotificationWithPosition notification,
            int dipFactor
        )
        {
            var scaled = new Vector2(
                notification.Position.X / dipFactor,
                notification.Position.Y / dipFactor
            );

            return scaled;
        }

        public static Vector2 ToPixelTopLeftSpace(
            this NotificationWithPosition notification,
            int pixelFactor
        )
        {
            var scaled = new Vector2(
                notification.Position.X / pixelFactor,
                notification.Position.Y / pixelFactor
            );

            return scaled;
        }
    }
}
