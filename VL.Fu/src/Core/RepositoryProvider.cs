using VL.Fu.Core.Repository;

namespace VL.Fu.Core
{
    /// <summary>
    /// A base class that provides service repository functionality to a derived class (like Fu).
    /// It manages the lifecycle and registration of services scoped to its own unique instance ID.
    /// </summary>
    /// <remarks>
    public abstract class RepositoryProvider : InstanceIdBase, IRepositoryService
    {
        protected static readonly ServiceRepository _repository = new();

        /// <summary>
        /// Registers a service with this provider's context. This method is virtual
        /// so that derived classes can extend the registration behavior.
        /// </summary>
        protected virtual void Register(IRepositoryService service)
        {
            _repository.RegisterService(InstanceId, service);
        }

        public T? GetService<T>()
            where T : class, IRepositoryService
        {
            return _repository.GetService<T>(InstanceId);
        }

        public virtual void Dispose()
        {
            _repository.UnregisterServices(InstanceId);
        }
    }
}
