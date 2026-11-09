using Microsoft.Extensions.Logging;
using VL.Core;
using VL.Core.Import;
using VL.Lang;
using VL.Model;

namespace VL.Fu.Core.Base
{
    internal interface INodeBase : IDisposable
    {
        void AddPersistentWarning(string message, TimeSpan? timeSpan = null);

        void Log(LogLevel level, string message, TimeSpan? lifetime = null);
    }

    public abstract class NodeBase : INodeBase
    {
        protected static readonly TimeSpan DefaultWarningLifetime = TimeSpan.FromSeconds(10);

        private readonly object _warningLock = new();
        private IDisposable? _warning;
        private string? _warningMessage;
        private MessageSeverity _warningSeverity;
        private CancellationTokenSource? _warningExpiry;

        public TimeSpan WarningLifetime { get; set; } = DefaultWarningLifetime;

        protected NodeContext? NodeContext { get; private set; }
        protected ILogger? Logger => NodeContext?.GetLogger();

        public NodeBase([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
        {
            NodeContext = nodeContext;
        }

        /// <summary>
        /// Shows a warning on the node in the patch, replacing any message shown before.
        /// The warning is removed after <paramref name="lifetime"/>, or <see cref="WarningLifetime"/> when not given.
        /// Pass <see cref="Timeout.InfiniteTimeSpan"/> to keep it until <see cref="ClearWarning"/> or disposal.
        /// </summary>
        public void AddPersistentWarning(string message, TimeSpan? lifetime = null)
        {
            ShowMessage(MessageSeverity.Warning, message, lifetime);
        }

        /// <summary>
        /// Reports a problem without interrupting the patch. The message always goes to the node's logger.
        /// Warnings and above are also shown on the node in the patch, like <see cref="AddPersistentWarning"/>.
        /// Repeating the message that is currently shown only extends its lifetime and is not logged again,
        /// so it is safe to call every frame while a problem persists.
        /// </summary>
        public virtual void Log(LogLevel level, string message, TimeSpan? lifetime = null)
        {
            var severity = ToSeverity(level);
            if (severity >= MessageSeverity.Warning && !ShowMessage(severity, message, lifetime))
                return;

            Logger?.Log(level, message);
        }

        /// <summary>
        /// Removes the current message, if any, and stops its expiry timer.
        /// </summary>
        public void ClearWarning()
        {
            lock (_warningLock)
                ClearWarningCore();
        }

        // Returns false when the same message was already shown and only its lifetime got extended.
        private bool ShowMessage(MessageSeverity severity, string message, TimeSpan? lifetime)
        {
            var duration = lifetime ?? WarningLifetime;

            lock (_warningLock)
            {
                if (_warning is not null && _warningSeverity == severity && _warningMessage == message)
                {
                    RestartExpiry(_warning, duration);
                    return false;
                }

                ClearWarningCore();

                // Without a context (e.g. constructed from C# or in tests) there is no patch to show it on.
                if (NodeContext is null)
                    return true;

                var warning = NodeContext.AddPersistentMessage(severity, message);
                _warning = warning;
                _warningMessage = message;
                _warningSeverity = severity;

                RestartExpiry(warning, duration);
                return true;
            }
        }

        private void RestartExpiry(IDisposable warning, TimeSpan duration)
        {
            StopExpiry();

            if (duration == Timeout.InfiniteTimeSpan)
                return;

            var expiry = new CancellationTokenSource();
            _warningExpiry = expiry;
            _ = ExpireAsync(warning, duration, expiry.Token);
        }

        private void StopExpiry()
        {
            _warningExpiry?.Cancel();
            _warningExpiry?.Dispose();
            _warningExpiry = null;
        }

        private void ClearWarningCore()
        {
            StopExpiry();

            _warning?.Dispose();
            _warning = null;
            _warningMessage = null;
        }

        private static MessageSeverity ToSeverity(LogLevel level) =>
            level switch
            {
                LogLevel.Warning => MessageSeverity.Warning,
                LogLevel.Error => MessageSeverity.Error,
                LogLevel.Critical => MessageSeverity.Critical,
                LogLevel.None => MessageSeverity.None,
                _ => MessageSeverity.Info,
            };

        // Awaits without ConfigureAwait(false) so the message is removed on the VL main thread when called from there.
        private async Task ExpireAsync(IDisposable warning, TimeSpan delay, CancellationToken token)
        {
            try
            {
                await Task.Delay(delay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            lock (_warningLock)
            {
                // A newer warning may have replaced this one in the meantime; that one owns its own expiry.
                if (ReferenceEquals(_warning, warning))
                    ClearWarningCore();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Override to release resources of derived nodes. Always call the base implementation.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                ClearWarning();
        }
    }
}
