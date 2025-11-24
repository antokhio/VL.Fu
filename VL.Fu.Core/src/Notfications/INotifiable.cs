using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Notfications
{
    public interface INotifiable
    {
        void Notify(INotification notification);
    }
}
