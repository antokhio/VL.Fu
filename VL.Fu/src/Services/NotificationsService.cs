using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Core.Notifications;
using VL.Fu.Core.Repository;
using VL.Fu.Extensions;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services
{
    /// <summary>
    /// A reactive service that transforms a raw stream of INotifications into structured,
    /// cached collections of input state for frame-by-frame consumption.
    /// </summary>
    public class NotificationService : IRepositoryService, INotifiable
    {
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Subject<INotification> _notifications = new();

        public IReadOnlyDictionary<int, FuPointer> Pointers { get; private set; } =
            ImmutableDictionary<int, FuPointer>.Empty;
        public IReadOnlySet<FuKey> KeysDown { get; private set; } = ImmutableHashSet<FuKey>.Empty;
        public IReadOnlySet<FuKey> ModifiersDown { get; private set; } =
            ImmutableHashSet<FuKey>.Empty;

        public IObservable<IReadOnlyDictionary<int, FuPointer>> PointersStream { get; }
        public IObservable<IReadOnlySet<FuKey>> KeysDownStream { get; }

        public NotificationService(ConfigurationBase configuration)
        {
            var pixelFactor = configuration.PixelFactor;

            var enabledNotifications = _notifications
                .Where(n => configuration.Enabled.Value)
                .Publish()
                .RefCount();

            var resetEvents = _notifications
                .OfType<LostFocusNotification>()
                .Select(_ => Unit.Default)
                .Merge(configuration.Enabled.Where(enabled => !enabled).Select(_ => Unit.Default));

            var pointerEvents = enabledNotifications
                .OfType<NotificationWithPosition>()
                .Select(notification =>
                {
                    var space = configuration.Space.Value;
                    var dipFactor = configuration.DIPFactor.Value;
                    return (object)notification.ToFuPointer(space, dipFactor, pixelFactor);
                })
                .Where(p => (FuPointer)p != FuPointer.Default);

            var combinedStream = pointerEvents.Merge(resetEvents.Select(e => (object)e));

            // Create the stream with the scanning logic.
            PointersStream = combinedStream
                .Scan(
                    ImmutableDictionary<int, FuPointer>.Empty,
                    (pointers, ev) =>
                    {
                        if (ev is Unit)
                        {
                            return ImmutableDictionary<int, FuPointer>.Empty;
                        }

                        var cleanedPointers = pointers.RemoveRange(
                            pointers
                                .Where(kvp => kvp.Value.State == TouchNotificationKind.TouchUp)
                                .Select(kvp => kvp.Key)
                        );

                        var pointer = (FuPointer)ev;

                        return pointer.State switch
                        {
                            TouchNotificationKind.TouchDown => cleanedPointers.SetItem(
                                pointer.Id,
                                pointer
                            ),
                            TouchNotificationKind.TouchMove => cleanedPointers.TryGetValue(
                                pointer.Id,
                                out var existing
                            )
                                ? cleanedPointers.SetItem(
                                    pointer.Id,
                                    existing
                                        .WithState(TouchNotificationKind.TouchMove)
                                        .WithPosition(pointer.Position)
                                )
                                : cleanedPointers,
                            TouchNotificationKind.TouchUp => cleanedPointers.TryGetValue(
                                pointer.Id,
                                out var upExisting
                            )
                                ? cleanedPointers.SetItem(
                                    pointer.Id,
                                    upExisting.WithState(TouchNotificationKind.TouchUp)
                                )
                                : cleanedPointers,
                            _ => cleanedPointers,
                        };
                    }
                )
                .StartWith(ImmutableDictionary<int, FuPointer>.Empty)
                .Publish() // Make it a hot stream
                .RefCount(); // Start when the first subscriber arrives, stop when the last leaves.

            // Subscribe to the hot stream to update the polling property.
            _subscriptions.Add(PointersStream.Subscribe(p => Pointers = p));

            // --- Key Handling ---
            var keyEvents = enabledNotifications.OfType<KeyNotification>().Select(n => (object)n);
            var allKeysStream = keyEvents.Merge(resetEvents.Select(e => (object)e));

            // This stream now represents all keys being held down, including modifiers.
            KeysDownStream = allKeysStream
                .Scan(
                    ImmutableHashSet<FuKey>.Empty,
                    (keys, ev) =>
                    {
                        if (ev is Unit)
                        {
                            return ImmutableHashSet<FuKey>.Empty;
                        }

                        return (KeyNotification)ev switch
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
                .Publish()
                .RefCount();

            // Subscribe to KeysDownStream to update the polling properties.
            _subscriptions.Add(
                KeysDownStream.Subscribe(keys =>
                {
                    var keysDownBuilder = ImmutableHashSet.CreateBuilder<FuKey>();
                    var modifiersDownBuilder = ImmutableHashSet.CreateBuilder<FuKey>();
                    foreach (var key in keys)
                    {
                        if (key.IsModifier())
                            modifiersDownBuilder.Add(key);
                        else
                            keysDownBuilder.Add(key);
                    }
                    KeysDown = keysDownBuilder.ToImmutable();
                    ModifiersDown = modifiersDownBuilder.ToImmutable();
                })
            );
        }

        public void Notify(INotification notification) => _notifications.OnNext(notification);

        public void Dispose() => _subscriptions.Dispose();
    }
}
