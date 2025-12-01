using VL.Fu.Core.InstanceId;

namespace VL.Fu.Core.Repository
{
    /// <summary>
    /// Defines the contract for a node that provides a context for services.
    /// The provider's InstanceId is used as the context ID for all services it registers.
    /// It also acts as a service locator for its own context.
    /// </summary>
    public interface IRepositoryProvider : IInstanceId
    {
        /// <summary>
        /// Retrieves a service of the specified type from this provider's context.
        /// </summary>
        /// <typeparam name="T">The type of service to retrieve.</typeparam>
        /// <returns>The service instance, or null if not found.</returns>
        T? GetService<T>()
            where T : class, IRepositoryService;
    }
}
