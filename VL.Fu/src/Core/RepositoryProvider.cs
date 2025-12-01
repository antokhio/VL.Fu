using VL.Fu.Core.Repository;

namespace VL.Fu.Core
{
    /// <summary>
    /// A base class that provides service repository functionality to a derived class (like Fu).
    /// It manages the lifecycle and registration of services scoped to its own unique instance ID.
    /// </summary>
    /// <remarks>
    public abstract class RepositoryProvider
        : InstanceIdBase,
            IRepositoryService,
            IRepositoryProvider
    {
        /// <summary>
        /// Registers a service with this provider's context. This method is virtual
        /// so that derived classes can extend the registration behavior.
        /// </summary>
        protected virtual void Register(IRepositoryService service)
        {
            // Use the public singleton instance to register the service.
            ServiceRepository.Instance.RegisterService(this.InstanceId, service);
        }

        public T? GetService<T>()
            where T : class, IRepositoryService
        {
            return ServiceRepository.Instance.GetService<T>(this.InstanceId);
        }

        public virtual void Dispose()
        {
            ServiceRepository.Instance.UnregisterServices(this.InstanceId);
        }
    }
}
