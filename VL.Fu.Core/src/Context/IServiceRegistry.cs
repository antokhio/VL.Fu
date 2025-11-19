namespace VL.Fu.Core.Context
{
    public interface IServiceRegistry : IDisposable
    {
        T? GetService<T>()
            where T : class, IContextedService;

        void RegisterService<T>(T service)
            where T : class, IContextedService;
    }
}
