using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Context;
using VL.Lib.Control;

namespace VL.Fu
{
    [ProcessNode()]
    public class FuProvider
    {
        [Fragment]
        public bool Update(
            [Pin(Visibility = Model.PinVisibility.Hidden)] NodeContext nodeContext,
            out IContextProvider provider
        )
        {
            return TryGetProvider(nodeContext, out provider);
        }

        public static bool TryGetProvider(
            [Pin(Visibility = Model.PinVisibility.Hidden)] NodeContext nodeContext,
            out IContextProvider provider
        )
        {
            provider = ScopedValueStore.LookupByName<IContextProvider>(
                nodeContext,
                Constants.RootContextProviderName,
                true
            );

            return provider is not null;
        }
    }
}
