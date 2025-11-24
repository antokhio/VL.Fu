namespace VL.Fu.Core
{
    public interface IServiceRegistry : IContextConsumer, IDisposable
    {
        void RegisterService<T>(T service)
            where T : class, IContextedService;
    }
}
