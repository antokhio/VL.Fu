namespace VL.Fu.Core.Context
{
    public interface IContextProviderInlay
    {
        void Execute(IContextProvider provider, out IFuNode? root);
    }
}
