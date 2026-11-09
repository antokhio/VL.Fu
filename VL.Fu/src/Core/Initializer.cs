using VL.Core;
using VL.Core.CompilerServices;

[assembly: AssemblyInitializer(typeof(VL.Fu.Core.Initializer))]

namespace VL.Fu.Core
{
    public sealed class Initializer : AssemblyInitializer<Initializer>
    {
        public override void Configure(AppHost appHost)
        {
            // add yoga default config handling
            // appHost.RegisterService<YogaConfigService>(new YogaConfigService());

            base.Configure(appHost);
        }
    }
}
