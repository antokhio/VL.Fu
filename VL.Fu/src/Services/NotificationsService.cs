using System.Collections.Immutable;
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

        public NotificationService(ConfigurationBase configuration)
        {
            var pixelFactor = configuration.PixelFactor;

            // Only process notifications when the context is enabled
            var enabledNotifications = _notifications
                .WithLatestFrom(
                    configuration.Enabled,
                    (n, enabled) => (notification: n, enabled: enabled)
                )
                .Where(t => t.enabled)
                .Select(t => t.notification);

            // Stream of valid FuPointer events
            var pointerEvents = _notifications
                .WithLatestFrom(configuration.Space, (notification, space) => (notification, space))
                .WithLatestFrom(
                    configuration.DIPFactor,
                    (tuple, dipFactor) => (tuple.notification, tuple.space, dipFactor)
                )
                .Select(t => (object)t.notification.ToFuPointer(t.space, t.dipFactor, pixelFactor)) // Cast to object
                .Where(p => (FuPointer)p != FuPointer.Default);

            // Stream of focus lost events
            var lostFocusEvents = _notifications
                .OfType<LostFocusNotification>()
                .Select(n => (object)n);

            // Merge both streams. The Scan operator will now receive either a FuPointer or a LostFocusNotification.
            var pointersStream = pointerEvents
                .Merge(lostFocusEvents)
                .Scan(
                    ImmutableDictionary<int, FuPointer>.Empty,
                    (pointers, ev) =>
                    {
                        // If focus is lost, clear all pointers.
                        if (ev is LostFocusNotification)
                        {
                            return ImmutableDictionary<int, FuPointer>.Empty;
                        }

                        var pointer = (FuPointer)ev;

                        var cleanedPointers = pointers.RemoveRange(
                            pointers
                                .Where(kvp => kvp.Value.State == TouchNotificationKind.TouchUp)
                                .Select(kvp => kvp.Key)
                        );

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
                                    existing.WithPosition(pointer.Position)
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
                .StartWith(ImmutableDictionary<int, FuPointer>.Empty);

            _subscriptions.Add(pointersStream.Subscribe(p => Pointers = p));

            var allKeysDownStream = _notifications
                .OfType<KeyNotification>()
                .Scan(
                    ImmutableHashSet<FuKey>.Empty,
                    (keys, n) =>
                        n switch
                        {
                            KeyDownNotification kdn => keys.Add(new FuKey(kdn.KeyCode, kdn.Kind)),
                            KeyUpNotification kun => keys.Remove(
                                keys.FirstOrDefault(k => k.Key == kun.KeyCode)
                            ),
                            _ => keys,
                        }
                )
                .StartWith(ImmutableHashSet<FuKey>.Empty);

            // This separate subscription for keys is still fine.
            _subscriptions.Add(
                _notifications
                    .OfType<LostFocusNotification>()
                    .Subscribe(_ =>
                    {
                        KeysDown = ImmutableHashSet<FuKey>.Empty;
                        ModifiersDown = ImmutableHashSet<FuKey>.Empty;
                    })
            );

            _subscriptions.Add(
                allKeysDownStream.Subscribe(keys =>
                {
                    var keysDown = new HashSet<FuKey>();
                    var modifiersDown = new HashSet<FuKey>();
                    foreach (var key in keys)
                    {
                        if (key.IsModifier())
                            modifiersDown.Add(key);
                        else
                            keysDown.Add(key);
                    }
                    KeysDown = keysDown.ToImmutableHashSet();
                    ModifiersDown = modifiersDown.ToImmutableHashSet();
                })
            );
        }

        public void Notify(INotification notification) => _notifications.OnNext(notification);

        public void Dispose() => _subscriptions.Dispose();
    }
}
