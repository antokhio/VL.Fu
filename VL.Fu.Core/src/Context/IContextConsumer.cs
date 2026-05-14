namespace VL.Fu.Core
{
    public interface IContextConsumer
    {
        /// <summary>
        /// Retrieves a service of the specified type from the active context.
        /// Returns null if the service is not found or if the node is not in a valid context.
        /// </summary>
        T? GetService<T>()
            where T : class, IContextedService;
    }
}
