using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Linq;
using VL.Fu.Core.Input;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Handlers
{
    public class KeysHandler : IObservable<IReadOnlySet<FuKey>>
    {
        private readonly record struct KeyEventArgs(
            KeyNotification Notification = default,
            bool IsReset = false
        );

        protected readonly IObservable<IReadOnlySet<FuKey>> _activeStream;

        public KeysHandler(IObservable<INotification> notifications, IObservable<Unit> onReset)
        {
            var keyNotifications = notifications.OfType<KeyNotification>();

            var keyEvents = Observable.Merge(
                keyNotifications.Select(n => new KeyEventArgs(Notification: n)),
                onReset.Select(_ => new KeyEventArgs(IsReset: true))
            );

            _activeStream = keyEvents
                .Scan(
                    ImmutableHashSet<FuKey>.Empty,
                    (keys, input) =>
                    {
                        if (input.IsReset)
                        {
                            return ImmutableHashSet<FuKey>.Empty;
                        }

                        return input.Notification switch
                        {
                            KeyDownNotification kdn => keys.Add(new FuKey(kdn.KeyCode, kdn.Kind)),
                            KeyUpNotification kun => keys.Remove(
                                keys.FirstOrDefault(k => k.Key == kun.KeyCode)
                            ),

                            _ => keys,
                        };
                    }
                )
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
