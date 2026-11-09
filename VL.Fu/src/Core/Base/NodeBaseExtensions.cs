using Microsoft.Extensions.Logging;

namespace VL.Fu.Core.Base
{
    /// <summary>
    /// Shorthands for <see cref="NodeBase.Log"/> with a fixed level.
    /// Warnings and errors are also shown on the node in the patch; repeating the shown message only extends its lifetime.
    /// </summary>
    public static class NodeBaseExtensions
    {
        /// <summary>
        /// Writes <paramref name="message"/> to the node's logger only.
        /// Nothing is shown on the node in the patch.
        /// </summary>
        public static void LogInfo(this NodeBase node, string message) =>
            node.Log(LogLevel.Information, message);

        /// <summary>
        /// Shows <paramref name="message"/> as a warning on the node and writes it to the node's logger.
        /// The warning is removed after <paramref name="lifetime"/>, or <see cref="NodeBase.WarningLifetime"/> when not given.
        /// </summary>
        public static void LogWarning(this NodeBase node, string message, TimeSpan? lifetime = null) =>
            node.Log(LogLevel.Warning, message, lifetime);

        /// <summary>
        /// Shows <paramref name="message"/> as an error on the node and writes it to the node's logger.
        /// The error is removed after <paramref name="lifetime"/>, or <see cref="NodeBase.WarningLifetime"/> when not given.
        /// </summary>
        public static void LogError(this NodeBase node, string message, TimeSpan? lifetime = null) =>
            node.Log(LogLevel.Error, message, lifetime);
    }
}
