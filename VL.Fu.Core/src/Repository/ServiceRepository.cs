using System.Collections.Concurrent;

namespace VL.Fu.Core.Repository
{
    /// <summary>
    /// A thread-safe, singleton repository for managing and locating services scoped to specific instance contexts.
    /// </summary>
    public class ServiceRepository
    {
        public static readonly ServiceRepository Instance = new();

        private readonly ConcurrentDictionary<
            int,
            ConcurrentDictionary<Type, IRepositoryService>
        > _servicesByContext = new();

        // Private constructor to ensure it's only created once via the static Instance field.
        private ServiceRepository() { }

        public void RegisterService(int contextId, IRepositoryService service)
        {
            var contextServices = _servicesByContext.GetOrAdd(
                contextId,
                _ => new ConcurrentDictionary<Type, IRepositoryService>()
            );

            var serviceType = service.GetType();
            contextServices[serviceType] = service;

            var interfaces = serviceType
                .GetInterfaces()
                .Where(i => typeof(IRepositoryService).IsAssignableFrom(i));

            foreach (var interfaceType in interfaces)
            {
                contextServices[interfaceType] = service;
            }
        }

        public T? GetService<T>(int contextId)
            where T : class, IRepositoryService
        {
            if (
                _servicesByContext.TryGetValue(contextId, out var contextServices)
                && contextServices.TryGetValue(typeof(T), out var service)
            )
            {
                return service as T;
            }
            return null;
        }

        public void UnregisterServices(int contextId)
        {
            if (_servicesByContext.TryRemove(contextId, out var contextServices))
            {
                foreach (var service in contextServices.Values.Distinct())
                {
                    service.Dispose();
                }
                contextServices.Clear();
            }
        }
    }
}
