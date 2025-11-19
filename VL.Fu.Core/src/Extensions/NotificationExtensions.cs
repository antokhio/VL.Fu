using VL.Fu.Core.Input;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Extensions
{
    public static class NotificationExtensions
    {
        public static FuPointer ToPointer(this INotification notification, FuViewport viewport) =>
            notification switch
            {
                TouchNotification touchNotification => touchNotification.ToPointer(viewport),
                MouseNotification mouseNotification => mouseNotification.ToPointer(viewport),
                _ => throw new ArgumentOutOfRangeException(nameof(notification)),
            };

        public static FuPointer ToPointer(this TouchNotification notification, FuViewport viewport)
        {
            return new FuPointer();
        }

        public static FuPointer ToPointer(this MouseNotification notification, FuViewport viewport)
        {
            return new FuPointer();
        }
    }
}
