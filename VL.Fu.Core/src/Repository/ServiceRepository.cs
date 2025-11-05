using System.Collections.Concurrent;

namespace VL.Fu.Core.Repository
{
    /// <summary>
    /// A thread-safe repository for managing and locating services scoped to specific instance contexts.
    /// </summary>
    /// <remarks>
    /// Created by: antokhio
    /// Date: 2025-11-05
    /// </remarks>
    public class ServiceRepository
    {
        private readonly ConcurrentDictionary<
            int,
            ConcurrentDictionary<Type, IRepositoryService>
        > _servicesByContext = new();

        public void RegisterService(int contextId, IRepositoryService service)
        {
            var contextServices = _servicesByContext.GetOrAdd(
                contextId,
                _ => new ConcurrentDictionary<Type, IRepositoryService>()
            );
            contextServices[service.GetType()] = service;
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
                foreach (var service in contextServices.Values)
                {
                    service.Dispose();
                }
                contextServices.Clear();
            }
        }
    }
}
