using Microsoft.Extensions.Logging;
using VL.Core;
using VL.Fu.Core.Base;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Warnings from interaction calls that may come without a node, like a <c>Dispatch</c> made outside a pass.
    /// They are shown on the node when there is one, and otherwise written to the app host's logger.
    /// </summary>
    internal static class InteractionLog
    {
        public static void Warning(IInteractive? node, string message)
        {
            if (node is NodeBase nodeBase)
            {
                nodeBase.LogWarning(message);
                return;
            }

            try
            {
                AppHost.CurrentOrGlobal.DefaultLogger.LogWarning(message);
            }
            catch (InvalidOperationException)
            {
                // No app host is installed, as in tests or when the library is used from plain C#: there is nowhere to log to.
            }
        }
    }
}
