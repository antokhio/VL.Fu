namespace VL.Fu.Core.Repository
{
    public interface IRepositoryProvider
    {
        T? GetService<T>()
            where T : class, IRepositoryService;
    }
}
