namespace VL.Fu.Core
{
    public interface IServiceRegistry : IDisposable
    {
        T? GetService<T>()
            where T : class, IContextedService;

        void RegisterService<T>(T service)
            where T : class, IContextedService;
    }
}
