using VL.Fu.Core.Repository;

namespace VL.Fu.Core
{
    /// <summary>
    /// An abstract base class for nodes that need to consume services from a repository.
    /// It provides a mechanism to receive a context ID and retrieve services scoped to that context.
    /// </summary>
    /// <remarks>
    public abstract class RepositoryConsumer : IRepositoryConsumer
    {
        // A static reference to the one and only service repository.
        protected static readonly ServiceRepository _repository = new();

        private int _contextId;

        public int ContextId => _contextId;

        /// <summary>
        /// Sets the context ID for this consumer, linking it to its parent repository's service container.
        /// This is intended to be called by a traversal mechanism (e.g., InteractionService).
        /// </summary>
        /// <param name="contextId">The instance ID of the parent RepositoryBase (e.g., Fu).</param>
        public void SetContextId(int contextId)
        {
            _contextId = contextId;
        }

        /// <summary>
        /// Retrieves a service from the repository using the current context ID.
        /// </summary>
        /// <typeparam name="T">The type of service to retrieve.</typeparam>
        /// <returns>The service instance, or null if not found or if the context is not yet set.</returns>
        protected T? GetService<T>()
            where T : class, IRepositoryService
        {
            if (_contextId > 0)
            {
                return _repository.GetService<T>(_contextId);
            }
            return null;
        }
    }
}
