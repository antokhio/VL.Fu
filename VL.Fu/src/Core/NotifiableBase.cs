using VL.Fu.Core;
using VL.Fu.Core.Notifications;
using VL.Fu.Core.Repository;
using VL.Lib.IO.Notifications;

public abstract class NotifiableBase : ConfigurationBase
{
    private readonly List<INotifiable> _notifiableServices = new();

    /// <summary>
    /// Overrides the base Register method to add specialized logic for INotifiable services.
    /// </summary>
    protected override void Register(IRepositoryService service)
    {
        // 1. Call the base method to ensure the service is in the main repository.
        base.Register(service);

        // 2. Add extended behavior for notifiable services.
        if (service is INotifiable notifiable && !_notifiableServices.Contains(notifiable))
        {
            _notifiableServices.Add(notifiable);
        }
    }

    /// <summary>
    /// Broadcasts a notification to all registered notifiable services.
    /// </summary>
    protected void BroadcastNotification(INotification notification)
    {
        foreach (var service in _notifiableServices)
        {
            service.Notify(notification);
        }
    }
}
