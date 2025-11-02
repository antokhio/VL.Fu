using VL.Fu.Core;

namespace VL.Fu.Services
{
    public class InteractionService
    {
        public void Update(
            IFuNode root,
            IReadOnlyCollection<FuCursor> cursors,
            IReadOnlyCollection<FuKey> keys,
            IReadOnlyCollection<FuKey> modifiers
        )
        {
            // If have running behaviours:
            //  - advance behaviours
            //  - if all behaviours are Canceled or Finished

            // Check witch elements are hit
            // Form list of potentially runinng behaviours
            // Try activate behaviour
        }
    }
}
