using System.Reactive.Disposables;
using System.Reactive.Linq;
using VL.Lib.Reactive;

namespace VL.Fu.Core.Property
{
    /// <summary>
    /// A mutable channel that can be dynamically connected to an upstream source for two-way synchronization.
    /// It ensures that changes from the upstream channel are reflected locally, and local changes
    /// are propagated back to the upstream channel.
    /// </summary>
    /// <typeparam name="T">The type of the value to hold.</typeparam>
    public class ChannelProperty<T> : ChannelPropertyBase<T>
    {
        private IChannel<T>? _currentUpstreamChannel;
        private IDisposable _subscriptions = Disposable.Empty;
        private bool _isSyncing; // Re-entrancy guard to prevent infinite loops
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the ChannelProperty with an initial value.
        /// </summary>
        /// <param name="initialValue">The starting value for the property.</param>
        public ChannelProperty(T initialValue)
            : base(Channel.Create(initialValue)) { }

        /// <summary>
        /// Sets the upstream channel and establishes two-way data binding.
        /// When set, it disconnects from any previous upstream channel. If set to null,
        /// it operates as a standalone channel.
        /// </summary>
        public void SetChannel(IChannel<T>? channel = null)
        {
            if (ReferenceEquals(_currentUpstreamChannel, channel))
                return;

            // Dispose of old subscriptions
            _subscriptions.Dispose();

            _currentUpstreamChannel = channel;

            if (_currentUpstreamChannel != null)
            {
                var upstreamToInternal = _currentUpstreamChannel.Subscribe(newValue =>
                {
                    // If a sync is already happening, ignore this notification to prevent loops.
                    if (_isSyncing)
                        return;

                    _isSyncing = true;
                    try
                    {
                        _internalChannel.OnNext(newValue);
                    }
                    finally
                    {
                        _isSyncing = false;
                    }
                });

                var internalToUpstream = _internalChannel.Subscribe(newValue =>
                {
                    // If a sync is already happening, ignore this notification to prevent loops.
                    if (_isSyncing)
                        return;

                    _isSyncing = true;
                    try
                    {
                        _currentUpstreamChannel.OnNext(newValue);
                    }
                    finally
                    {
                        _isSyncing = false;
                    }
                });

                // Set the initial value from the upstream without causing a loop
                _isSyncing = true;
                _internalChannel.OnNext(_currentUpstreamChannel.Value);
                _isSyncing = false;

                _subscriptions = new CompositeDisposable(upstreamToInternal, internalToUpstream);
            }
            else
            {
                _subscriptions = Disposable.Empty;
            }
        }

        /// <summary>
        /// Disposes resources and disconnects from the upstream channel.
        /// </summary>
        public override void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            SetChannel(null); // This will dispose the subscriptions
            base.Dispose();
        }
    }
}
