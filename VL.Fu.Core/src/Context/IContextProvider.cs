using VL.Core.PublicAPI;

namespace VL.Fu.Core.Context
{
    public interface IContextProvider
        : IRegion<IContextProviderInlay>,
            IServiceRegistry,
            IDisposable
    {
        IFuNode? Root { get; }
    }
}
