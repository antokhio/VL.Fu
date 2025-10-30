using VL.Fu.Core;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Extensions
{
    public static class TouchNotificationExtensions
    {
        public static FuCursor ToNewFuCursor(this TouchNotification notification) =>
            new FuCursor(
                notification.Id,
                notification.PositionInProjectionSpace.ToSkiaSpace(),
                notification.Kind
            );

        public static FuCursor ToFuCursorWithDelta(
            this TouchNotification notification,
            FuCursor previous
        )
        {
            var position = notification.PositionInProjectionSpace.ToSkiaSpace();
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
