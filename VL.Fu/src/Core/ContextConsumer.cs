using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Context;
using VL.Lib.Control;

namespace VL.Fu.Core
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class ContextConsumer : InstancedId, IContextConsumer
    {
        protected readonly NodeContext NodeContext;

        // Cache the provider to avoid traversing the scope stack on every frame.
        private IContextProvider? _contextProvider;
        private bool _providerLookedUp = false;

        [Fragment]
        public ContextConsumer(NodeContext nodeContext)
        {
            NodeContext = nodeContext;
        }

        /// <summary>
        /// Retrieves the context provider from the node context.
        /// Results are cached after the first lookup.
        /// Returns null if the node is not placed within a valid context region.
        /// </summary>
        protected IContextProvider? ContextProvider
        {
            get
            {
                if (!_providerLookedUp)
                {
                    // We lookup IContextProvider because ContextProvider.cs ConfigureStore
                    // registers the scope with typeof(IContextProvider).
                    _contextProvider = ScopedValueStore.LookupByName<IContextProvider>(
                        NodeContext,
                        Constants.RootContextProviderName,
                        warn: true
                    );
                    _providerLookedUp = true;
                }
                return _contextProvider;
            }
        }

        /// <summary>
        /// Tries to retrieve a service of type T from the context provider.
        /// Returns null if the provider is missing or the service is not registered.
        /// </summary>
        public T? GetService<T>()
            where T : class, IContextedService
        {
            return ContextProvider?.GetService<T>();
        }

        public virtual void Dispose()
        {
            // Release the reference to the provider.
            // Note: We do not dispose the provider itself, as we are just a consumer.
            _contextProvider = null;
        }
    }
}
