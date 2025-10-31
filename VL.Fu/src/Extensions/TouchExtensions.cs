using VL.Fu.Core;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Extensions
{
    public static class TouchNotificationExtensions
    {
        public static FuCursor ToNewFuCursor(
            this TouchNotification notification,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        ) =>
            new FuCursor(
                notification.Id,
                notification.PositionInProjectionSpace.ToSkiaSpace(),
                notification.Kind
            );

        public static FuCursor ToFuCursorWithDelta(
            this TouchNotification notification,
            FuCursor previous,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        )
        {
            var position = notification.ToCommonSpace(space, dipFactor, pixelFactor);

            var delta = previous.Position - position;

            return previous with
            {
                Position = position,
                Delta = delta,
                State = notification.Kind,
                Distance = previous.Distance + delta,
            };
        }
    }
}
