using VL.Core.PublicAPI;

namespace VL.Fu.Core
{
    public interface IContextProvider
        : IRegion<IContextProviderInlay>,
            IServiceRegistry,
            IDisposable
    {
        IFuNode? Root { get; }
    }
}
