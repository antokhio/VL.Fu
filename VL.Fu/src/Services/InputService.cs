using Stride.Core.Mathematics;
using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Extensions;
using VL.Fu.Core.Input;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services
{
    public class InputService : IContextedService, INotifiable
    {
        public Subject<FuInputState> InputStateStream { get; } = new();
        public IObservable<IReadOnlyDictionary<int, FuPointer>> PointersStream { get; }
        public IObservable<FuMouse> MouseStream { get; }
        public IObservable<IReadOnlySet<FuKey>> KeysStream { get; }
        public IObservable<bool> IsTouchActiveStream { get; }

        private readonly Subject<INotification> _notifications = new();
        private readonly CompositeDisposable _subscriptions = new();

        private record CleanupPointer(int Id);

        public InputService(Configuration configuration, ViewportService viewportService)
        {
            var enabledNotifications = _notifications
                .Where(_ => configuration.Enabled.Value)
                .Publish()
                .RefCount();

            var touchNotifications = enabledNotifications.OfType<TouchNotification>();

            IsTouchActiveStream = touchNotifications
                .Select(_ => true)
                .Merge(
                    touchNotifications.Throttle(TimeSpan.FromMilliseconds(150)).Select(_ => false)
                )
                .StartWith(false)
                .Replay(1)
                .RefCount();

            var pointerEvents = touchNotifications
                .SelectMany(n =>
                {
                    var pointer = n.ToFuPointer(viewportService.Viewport);
                    if (n.Kind == TouchNotificationKind.TouchUp)
                    {
                        return
                        [
                            Observable.Return((object)pointer),
                            Observable
                                .Return((object)new CleanupPointer(pointer.Id))
                                .Delay(TimeSpan.FromMilliseconds(16)),
                        ];
                    }
                    return new[] { Observable.Return((object)pointer) };
                })
                .Merge();

            var pointerResetEvents = enabledNotifications
                .OfType<LostFocusNotification>()
                .Select(_ => (object)Unit.Default)
                .Merge(
                    configuration
                        .Enabled.Where(enabled => !enabled)
                        .Select(_ => (object)Unit.Default)
                );

            PointersStream = pointerEvents
                .Merge(pointerResetEvents)
                .Scan(
                    ImmutableDictionary<int, FuPointer>.Empty,
                    (pointers, ev) =>
                        ev switch
                        {
                            CleanupPointer cleanup => pointers.Remove(cleanup.Id),
                            Unit => ImmutableDictionary<int, FuPointer>.Empty,
                            FuPointer pointer => pointer.State switch
                            {
                                TouchNotificationKind.TouchDown => pointers.SetItem(
                                    pointer.Id,
                                    pointer
                                ),
                                TouchNotificationKind.TouchMove => pointers.TryGetValue(
                                    pointer.Id,
                                    out var existing
                                )
                                    ? pointers.SetItem(
                                        pointer.Id,
                                        existing.WithPosition(pointer.Position)
                                    )
                                    : pointers.SetItem(
                                        pointer.Id,
                                        pointer.WithState(TouchNotificationKind.TouchDown)
                                    ), // Orphaned move
                                TouchNotificationKind.TouchUp => pointers.TryGetValue(
                                    pointer.Id,
                                    out var upExisting
                                )
                                    ? pointers.SetItem(
                                        pointer.Id,
                                        upExisting.WithState(TouchNotificationKind.TouchUp)
                                    )
                                    : pointers,
                                _ => pointers,
                            },
                            _ => pointers,
                        }
                )
                .StartWith(ImmutableDictionary<int, FuPointer>.Empty);

            var mouseNotifications = enabledNotifications
                .OfType<MouseNotification>()
                .WithLatestFrom(
                    IsTouchActiveStream,
                    (n, isTouchActive) => (Notification: n, IsTouchActive: isTouchActive)
                )
                .Where(t => !t.IsTouchActive)
                .Select(t => t.Notification);

            MouseStream = mouseNotifications
                .Scan(
                    new FuMouse(),
                    (mouse, n) =>
                    {
                        var newPosition =
                            (n as NotificationWithPosition)?.ToCurrentSpace(
                                viewportService.Viewport
                            ) ?? mouse.Position;

                        var isLeft = mouse.IsLeft;
                        var isRight = mouse.IsRight;
                        var isMiddle = mouse.IsMiddle;
                        var isXButton1 = mouse.IsXButton1;
                        var isXButton2 = mouse.IsXButton2;
                        var buttons = MouseButtons.None;

                        if (n is MouseButtonNotification bn)
                        {
                            buttons = bn.Buttons;
                            if (n.Kind == MouseNotificationKind.MouseDown)
                            {
                                if (bn.Buttons.HasFlag(MouseButtons.Left))
                                    isLeft = true;
                                if (bn.Buttons.HasFlag(MouseButtons.Right))
                                    isRight = true;
                                if (bn.Buttons.HasFlag(MouseButtons.Middle))
                                    isMiddle = true;
                                if (bn.Buttons.HasFlag(MouseButtons.XButton1))
                                    isXButton1 = true;
                                if (bn.Buttons.HasFlag(MouseButtons.XButton2))
                                    isXButton2 = true;
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
                                    isXButton1 = false;
                                if (bn.Buttons.HasFlag(MouseButtons.XButton2))
                                    isXButton2 = false;
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
                            Wheel = mouse.Wheel + wheelDelta,
                            WheelDelta = wheelDelta,
                            IsLeft = isLeft,
                            IsRight = isRight,
                            IsMiddle = isMiddle,
                            IsXButton1 = isXButton1,
                            IsXButton2 = isXButton2,
                            Buttons = buttons,
                            State = n.Kind,
                        };
                    }
                )
                .StartWith(new FuMouse())
                .Publish()
                .RefCount();

            // --- Key Handling ---
            var keyResetEvents = enabledNotifications
                .OfType<LostFocusNotification>()
                .Select(_ => (object)Unit.Default)
                .Merge(
                    configuration
                        .Enabled.Where(enabled => !enabled)
                        .Select(_ => (object)Unit.Default)
                );

            var keyEvents = enabledNotifications.OfType<KeyNotification>().Select(n => (object)n);

            KeysStream = keyEvents
                .Merge(keyResetEvents)
                .Scan(
                    ImmutableHashSet<FuKey>.Empty,
                    (keys, ev) =>
                    {
                        if (ev is Unit)
                        {
                            return ImmutableHashSet<FuKey>.Empty;
                        }

                        var n = (KeyNotification)ev;
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

            // --- Combine all streams into a single FuInputState ---
            var combinedInputStream = Observable.CombineLatest(
                PointersStream,
                MouseStream,
                KeysStream,
                (pointers, mouse, keysDown) =>
                {
                    // Cast to the concrete immutable type to allow modifications
                    var allPointers = (ImmutableDictionary<int, FuPointer>)pointers;

                    if (mouse.IsLeft || mouse.IsRight || mouse.IsMiddle)
                    {
                        var mouseState = mouse.State switch
                        {
                            MouseNotificationKind.MouseDown => TouchNotificationKind.TouchDown,
                            MouseNotificationKind.MouseUp => TouchNotificationKind.TouchUp,
                            _ => TouchNotificationKind.TouchMove,
                        };

                        // Update existing mouse pointer or add a new one
                        allPointers = allPointers.TryGetValue(
                            Constants.MousePointerId,
                            out var existing
                        )
                            ? allPointers.SetItem(
                                Constants.MousePointerId,
                                existing.WithPosition(mouse.Position).WithState(mouseState)
                            )
                            : allPointers.SetItem(
                                Constants.MousePointerId,
                                new FuPointer
                                {
                                    Id = Constants.MousePointerId,
                                    Position = mouse.Position,
                                    State = mouseState,
                                    TimeStamp = DateTimeOffset.UtcNow,
                                }
                            );
                    }
                    else
                    {
                        allPointers = allPointers.Remove(Constants.MousePointerId);
                    }

                    var keys = ImmutableHashSet.CreateBuilder<FuKey>();
                    var modifiers = ImmutableHashSet.CreateBuilder<FuKey>();
                    foreach (var key in keysDown)
                    {
                        if (key.IsModifier())
                            modifiers.Add(key);
                        else
                            keys.Add(key);
                    }

                    return new FuInputState(
                        allPointers,
                        keys.ToImmutable(),
                        modifiers.ToImmutable(),
                        mouse
                    );
                }
            );

            _subscriptions.Add(combinedInputStream.Subscribe(InputStateStream));
        }

        public void Notify(INotification notification) => _notifications.OnNext(notification);

        public void Dispose()
        {
            _notifications.OnCompleted();
            _subscriptions.Dispose();
        }
    }
}
