using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core
{
    public interface INotifiable
    {
        void Notify(INotification notification, CallerInfo caller);
    }
}
