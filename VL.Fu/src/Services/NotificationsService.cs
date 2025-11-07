using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Stride.Core.Mathematics;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Core.Notifications;
using VL.Fu.Core.Repository;
using VL.Fu.Extensions;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services
{
    public class NotificationService : IRepositoryService, INotifiable
    {
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Subject<INotification> _notifications = new();

        // --- POINTERS API (Touch only) ---
        public IReadOnlyDictionary<int, FuPointer> Pointers { get; private set; } =
            ImmutableDictionary<int, FuPointer>.Empty;
        public IObservable<IReadOnlyDictionary<int, FuPointer>> PointersStream { get; }

        // --- KEYS API ---
        public IReadOnlySet<FuKey> KeysDown { get; private set; } = ImmutableHashSet<FuKey>.Empty;
        public IReadOnlySet<FuKey> ModifiersDown { get; private set; } =
            ImmutableHashSet<FuKey>.Empty;
        public IObservable<IReadOnlySet<FuKey>> KeysDownStream { get; }

        // --- MOUSE API ---
        public FuMouse Mouse { get; private set; }
        public IObservable<FuMouse> MouseStream { get; }

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

            // --- Pointer Handling (Now Touch-Only) ---
            var pointerEvents = enabledNotifications
                .OfType<TouchNotification>() // Explicitly only use TouchNotification
                .Select(notification =>
                {
                    var space = configuration.Space.Value;
                    var dipFactor = configuration.DIPFactor.Value;
                    return (object)notification.ToFuPointer(space, dipFactor, pixelFactor);
                })
                .Where(p => (FuPointer)p != FuPointer.Default);

            var combinedPointerStream = pointerEvents.Merge(resetEvents.Select(e => (object)e));

            PointersStream = combinedPointerStream
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
                .Publish()
                .RefCount();
            _subscriptions.Add(PointersStream.Subscribe(p => Pointers = p));

            // --- Mouse Handling ---
            var mouseNotifications = enabledNotifications.OfType<MouseNotification>();

            MouseStream = mouseNotifications
                .Scan(
                    new FuMouse(),
                    (mouse, n) =>
                    {
                        var space = configuration.Space.Value;
                        var dipFactor = configuration.DIPFactor.Value;
                        var newPosition =
                            (n as NotificationWithPosition)?.ToCommonSpace(
                                space,
                                dipFactor,
                                pixelFactor
                            ) ?? mouse.Position;

                        var isLeft = mouse.IsLeft;
                        var isRight = mouse.IsRight;
                        var isMiddle = mouse.IsMiddle;
                        var isBack = mouse.IsXButton1;
                        var isForward = mouse.IsXButton2;
                        var lastButton = MouseButtons.None;

                        if (n is MouseButtonNotification bn)
                        {
                            lastButton = bn.Buttons;
                            if (n.Kind == MouseNotificationKind.MouseDown)
                            {
                                if (bn.Buttons.HasFlag(MouseButtons.Left))
                                    isLeft = true;
                                if (bn.Buttons.HasFlag(MouseButtons.Right))
                                    isRight = true;
                                if (bn.Buttons.HasFlag(MouseButtons.Middle))
                                    isMiddle = true;
                                if (bn.Buttons.HasFlag(MouseButtons.XButton1))
                                    isBack = true;
                                if (bn.Buttons.HasFlag(MouseButtons.XButton2))
                                    isForward = true;
                            }
                            else if (n.Kind == MouseNotificationKind.MouseUp)
                            {
                                if (bn.Buttons.HasFlag(MouseButtons.Left))
                                    isLeft = false;
                                if (bn.Buttons.HasFlag(MouseButtons.Right))
                                    isRight = false;
                                if (bn.Buttons.HasFlag(MouseButtons.Middle))
                                    isMiddle = false;
                                if (bn.Buttons.HasFlag(MouseButtons.XButton1))
                                    isBack = false;
                                if (bn.Buttons.HasFlag(MouseButtons.XButton2))
                                    isForward = false;
                            }
                        }

                        var wheelDelta = Int2.Zero;
                        if (n is MouseWheelNotification wn)
                            wheelDelta.Y = wn.WheelDelta;
                        if (n is MouseHorizontalWheelNotification hwn)
                            wheelDelta.X = hwn.WheelDelta;

                        return new FuMouse
                        {
                            Position = newPosition,
                            WheelDelta = wheelDelta,
                            IsLeft = isLeft,
                            IsRight = isRight,
                            IsMiddle = isMiddle,
                            IsXButton1 = isBack,
                            IsXButton2 = isForward,
                            Buttons = lastButton,
                            State = n.Kind,
                        };
                    }
                )
                .StartWith(new FuMouse())
                .Publish()
                .RefCount();

            _subscriptions.Add(MouseStream.Subscribe(m => Mouse = m));

            // --- Key Handling (Unchanged) ---
            KeysDownStream = enabledNotifications
                .OfType<KeyNotification>()
                .Merge(resetEvents.Select(e => (object)e))
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
