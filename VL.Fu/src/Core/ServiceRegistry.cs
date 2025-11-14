using System.Collections.Concurrent;
using VL.Core.Import;
using VL.Fu.Core.Context;

namespace VL.Fu.Core
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class ServiceRegistry : InstancedId, IServiceRegistry
    {
        private readonly ConcurrentDictionary<
            Type,
            WeakReference<IContextedService>
        > _serviceRegistry = new();

        [Fragment]
        public ServiceRegistry() { }

        public virtual void RegisterService<T>(T service)
            where T : class, IContextedService
        {
            var type = typeof(T);
            var weakRef = new WeakReference<IContextedService>(service);
            _serviceRegistry.AddOrUpdate(type, weakRef, (_, __) => weakRef);
        }

        public T? GetService<T>()
            where T : class, IContextedService
        {
            if (_serviceRegistry.TryGetValue(typeof(T), out var serviceReference))
            {
                if (serviceReference.TryGetTarget(out var contextService))
                {
                    return contextService as T;
                }
                //Clean up dead reference
                _serviceRegistry.TryRemove(typeof(T), out _);
            }
            return null;
        }

        public virtual void Dispose()
        {
            _serviceRegistry?.Clear();
        }
    }
}
