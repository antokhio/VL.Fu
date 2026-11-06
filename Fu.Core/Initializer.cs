using VL.Core;
using VL.Core.CompilerServices;

[assembly: AssemblyInitializer(typeof(Fu.Core.Initializer))]

namespace Fu.Core
{
    public sealed class Initializer : AssemblyInitializer<Initializer>
    {
        public override void Configure(AppHost appHost)
        {
            // add yoga default config handling

            base.Configure(appHost);
        }
    }
}
