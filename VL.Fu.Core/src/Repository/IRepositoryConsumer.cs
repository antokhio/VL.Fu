namespace VL.Fu.Core.Repository
{
    /// <summary>
    /// Defines the contract for an object that can consume services from a ServiceRepository.
    /// It provides a mechanism to receive a context ID.
    /// </summary>
    public interface IRepositoryConsumer
    {
        /// <summary>
        /// Sets the repository context ID for this consumer.
        /// </summary>
        /// <param name="contextId">The instance ID of the parent RepositoryBase (e.g., Fu).</param>
        void SetContextId(int contextId);

        /// <summary>
        /// Get's repository context ID for this consumer.
        /// </summary>
        int ContextId { get; }
    }
}
