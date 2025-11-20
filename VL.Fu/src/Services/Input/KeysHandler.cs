using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Linq;
using VL.Fu.Core.Input;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services.Input
{
    public class KeysHandler : IObservable<IReadOnlySet<FuKey>>
    {
        protected readonly IObservable<IReadOnlySet<FuKey>> _activeStream;

        public KeysHandler(IObservable<INotification> notifications, IObservable<Unit> resetSignal)
        {
            var keyStream = notifications
                .OfType<KeyNotification>()
                .Scan(
                    ImmutableHashSet<FuKey>.Empty,
                    (keys, reset) =>
                    {
                        return keys;
                    }
                );

            var resetEvent = resetSignal.Select(_ => ImmutableHashSet<FuKey>.Empty);

            _activeStream = Observable
                .Merge(keyStream, resetEvent)
                .StartWith(ImmutableHashSet<FuKey>.Empty)
                .DistinctUntilChanged()
                .Replay(1)
                .RefCount();
        }

        public IDisposable Subscribe(IObserver<IReadOnlySet<FuKey>> observer)
        {
            return _activeStream.Subscribe(observer);
        }
    }
}
