namespace VL.Fu.Core
{
    public interface IContextProviderInlay
    {
        void Execute(IContextProvider provider, out IFuNode? root);
    }
}
