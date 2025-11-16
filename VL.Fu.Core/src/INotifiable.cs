using VL.Lib.IO.Notifications;

namespace VL.Fu.Core
{
    public interface INotifiable
    {
        void Notify(INotification notification);
    }
}
