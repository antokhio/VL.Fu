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
    public class InputService : InstancedId, IInputService
    {
        public Subject<FuInputState> InputStateStream { get; } = new Subject<FuInputState>();
        public IObservable<bool> IsTouchActiveStream { get; }
        public IObservable<IReadOnlyDictionary<int, FuPointer>> PointersStream { get; }
        public IObservable<FuMouse> MouseStream { get; }
        public IObservable<IReadOnlySet<FuKey>> KeysStream { get; }

        private readonly Subject<INotification> _notifications = new();
        private readonly CompositeDisposable _subscriptions = new();

        private record PointerEvent
        {
            public FuPointer Pointer { get; init; }
        }

        private record CleanupPointer(int Id);

        public InputService(Configuration configuration, IViewportService viewportService)
        {
            var enabledNotifications = _notifications
                .Where(_ => configuration.Enabled.Value)
                .Publish()
                .RefCount();

            // --- Touch Active Stream ---
            var touchNotifications = enabledNotifications.OfType<TouchNotification>();
            IsTouchActiveStream = touchNotifications
                .Select(_ => true)
                .Merge(
                    touchNotifications.Throttle(TimeSpan.FromMilliseconds(150)).Select(_ => false)
                )
                .StartWith(false)
                .Replay(1)
                .RefCount();

            // --- Mouse Handling ---
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

            // --- Pointer Handling (Touch and Mouse) ---
            var touchPointerEvents = touchNotifications
                .SelectMany(n =>
                {
                    var pointer = n.ToFuPointer(viewportService.Viewport);
                    if (n.Kind == TouchNotificationKind.TouchUp)
                    {
                        return new[]
                        {
                            Observable.Return((object)new PointerEvent { Pointer = pointer }),
                            Observable
                                .Return((object)new CleanupPointer(pointer.Id))
                                .Delay(TimeSpan.FromMilliseconds(16)),
                        };
                    }
                    return new[]
                    {
                        Observable.Return((object)new PointerEvent { Pointer = pointer }),
                    };
                })
                .Merge();

            var mouseAsPointerEvents = MouseStream
                .Select(mouse =>
                {
                    var hasButtons = mouse.IsLeft || mouse.IsRight || mouse.IsMiddle;
                    var pointerState = mouse.State switch
                    {
                        MouseNotificationKind.MouseDown => TouchNotificationKind.TouchDown,
                        MouseNotificationKind.MouseUp => TouchNotificationKind.TouchUp,
                        _ => TouchNotificationKind.TouchMove,
                    };

                    var pointer = new FuPointer(
                        Constants.MousePointerId,
                        mouse.Position,
                        pointerState
                    );

                    return hasButtons ? new PointerEvent { Pointer = pointer } : null;
                })
                .DistinctUntilChanged() // Only emit when the pointer state changes (e.g., button down/up)
                .SelectMany(pointerEvent =>
                {
                    if (pointerEvent is null)
                    {
                        // When mouse buttons are released, issue a cleanup event
                        return new[]
                        {
                            Observable.Return<object>(new CleanupPointer(Constants.MousePointerId)),
                        };
                    }
                    if (pointerEvent.Pointer.State == TouchNotificationKind.TouchUp)
                    {
                        // For mouse up, also issue a delayed cleanup
                        return new[]
                        {
                            Observable.Return<object>(pointerEvent),
                            Observable
                                .Return<object>(new CleanupPointer(pointerEvent.Pointer.Id))
                                .Delay(TimeSpan.FromMilliseconds(16)),
                        };
                    }
                    return new[] { Observable.Return<object>(pointerEvent) };
                })
                .Merge();

            var allPointerEvents = Observable.Merge(touchPointerEvents, mouseAsPointerEvents);

            var pointerResetEvents = enabledNotifications
                .OfType<LostFocusNotification>()
                .Select(_ => (object)Unit.Default)
                .Merge(
                    configuration
                        .Enabled.Where(enabled => !enabled)
                        .Select(_ => (object)Unit.Default)
                );

            PointersStream = allPointerEvents
                .Merge(pointerResetEvents)
                .Scan(
                    ImmutableDictionary<int, FuPointer>.Empty,
                    (pointers, ev) =>
                        ev switch
                        {
                            CleanupPointer cleanup => pointers.Remove(cleanup.Id),
                            Unit => ImmutableDictionary<int, FuPointer>.Empty,
                            PointerEvent pe => pe.Pointer.State switch
                            {
                                TouchNotificationKind.TouchDown => pointers.SetItem(
                                    pe.Pointer.Id,
                                    pe.Pointer
                                ),
                                TouchNotificationKind.TouchMove => pointers.TryGetValue(
                                    pe.Pointer.Id,
                                    out var existing
                                )
                                    ? pointers.SetItem(
                                        pe.Pointer.Id,
                                        existing.WithPosition(pe.Pointer.Position)
                                    )
                                    : pointers.SetItem(
                                        pe.Pointer.Id,
                                        pe.Pointer.WithState(TouchNotificationKind.TouchDown)
                                    ), // Orphaned move
                                TouchNotificationKind.TouchUp => pointers.TryGetValue(
                                    pe.Pointer.Id,
                                    out var upExisting
                                )
                                    ? pointers.SetItem(
                                        pe.Pointer.Id,
                                        upExisting.WithState(TouchNotificationKind.TouchUp)
                                    )
                                    : pointers,
                                _ => pointers,
                            },
                            _ => pointers,
                        }
                )
                .StartWith(ImmutableDictionary<int, FuPointer>.Empty)
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
                            return ImmutableHashSet<FuKey>.Empty;

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

            // --- Combine all public streams into a single FuInputState ---
            var combinedInputStream = Observable.CombineLatest(
                PointersStream,
                MouseStream,
                KeysStream,
                (pointers, mouse, keysDown) =>
                {
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
                        pointers,
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
            _notifications.Dispose();
            _subscriptions.Dispose();
            InputStateStream.Dispose();
        }
    }
}
