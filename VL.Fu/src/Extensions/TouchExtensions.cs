using VL.Fu.Core;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Extensions
{
    public static class TouchExtensions
    {
        public static Cursor ToCursor(this TouchNotification notification) =>
            new Cursor(
                notification.Id,
                notification.PositionInProjectionSpace.ToSkiaSpace(),
                notification.Kind
            );
    }
}
