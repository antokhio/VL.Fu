using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Root;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class NotificationsProvider : ContextProvider
    {
        protected readonly List<INotifiable> _notifiables = new();

        [Fragment]
        public NotificationsProvider(NodeContext nodeContext)
            : base(nodeContext) { }

        public override void RegisterService<T>(T service)
        {
            base.RegisterService(service);

            if (service is INotifiable notifiable && !_notifiables.Contains(notifiable))
            {
                _notifiables.Add(notifiable);
            }
        }

        public void BroadcastNotification(INotification notification, CallerInfo caller)
        {
            foreach (var notifiable in _notifiables)
            {
                notifiable.Notify(notification, caller);
            }
        }
    }
}
