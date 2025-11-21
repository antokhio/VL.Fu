using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core.Common;
using VL.Fu.Core.Extensions;
using VL.Fu.Core.Input;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Handlers
{
    public class PointersHandler : IObservable<IReadOnlyDictionary<int, FuPointer>>
    {
        private readonly record struct MouseEventArgs(
            FuMouse State = default,
            bool IsReset = false,
            bool IsCleanup = false,
            int CleanupPointerId = default
        );

        private readonly record struct TouchEventArgs(
            TouchNotification Notification = default,
            FuViewport Viewport = default,
            bool IsReset = false,
            bool IsCleanup = false,
            int CleanupPointerId = default
        );

        protected readonly IObservable<IReadOnlyDictionary<int, FuPointer>> _activeStream;

        public PointersHandler(
            IObservable<INotification> notifications,
            IObservable<FuMouse> mouse,
            IObservable<bool> touchActive,
            IObservable<FuViewport> viewport,
            IObservable<Unit> onReset
        )
        {
            var mouseCleanupEvent = new Subject<Unit>();
            var touchCleanupEvent = new Subject<Unit>();

            var mouseEvents = Observable.Merge(
                mouse.Select(m => new MouseEventArgs(State: m)),
                onReset.Select(_ => new MouseEventArgs(IsReset: true)),
                mouseCleanupEvent.Select(_ => new MouseEventArgs(
                    IsCleanup: true,
                    CleanupPointerId: Constants.MousePointerId
                ))
            );

            var mousePointers = mouseEvents
                .Scan(
                    ImmutableDictionary<int, FuPointer>.Empty,
                    (pointers, ev) =>
                    {
                        if (ev.IsCleanup)
                        {
                            var builder = pointers.ToBuilder();
                            foreach (var kvp in pointers)
                            {
                                if (kvp.Value.State == TouchNotificationKind.TouchUp)
                                    builder.Remove(kvp.Key);
                            }
                            return builder.ToImmutable();
                        }

                        if (ev.IsReset)
                        {
                            var builder = pointers.ToBuilder();
                            foreach (var kvp in pointers)
                            {
                                if (kvp.Value.State != TouchNotificationKind.TouchUp)
                                {
                                    builder[kvp.Key] = kvp.Value.WithState(
                                        TouchNotificationKind.TouchUp
                                    );
                                }
                            }
                            return builder.ToImmutable();
                        }

                        if (pointers.TryGetValue(Constants.MousePointerId, out var pointer))
                        {
                            if (pointer.IsLeft is false && ev.State.IsLeft)
                            {
                                return pointers.SetItem(
                                    pointer.Id,
                                    pointer.WithMouseState(
                                        ev.State,
                                        TouchNotificationKind.TouchDown
                                    )
                                );
                            }
                            else if (pointer.IsLeft && ev.State.IsLeft is false)
                            {
                                return pointers.SetItem(
                                    pointer.Id,
                                    pointer.WithMouseState(ev.State, TouchNotificationKind.TouchUp)
                                );
                            }
                            else
                            {
                                return pointers.SetItem(
                                    pointer.Id,
                                    pointer.WithMouseState(
                                        ev.State,
                                        TouchNotificationKind.TouchMove
                                    )
                                );
                            }
                        }
                        else
                        {
                            pointer = FuPointer.DefaultMousePointer.WithMouseState(
                                ev.State,
                                TouchNotificationKind.TouchDown
                            );

                            return pointers.SetItem(pointer.Id, pointer);
                        }
                    }
                )
                .Do(pointers =>
                {
                    if (
                        pointers.Values.Any(pointer =>
                            pointer.State is TouchNotificationKind.TouchUp
                        )
                    )
                    {
                        Scheduler.Default.Schedule(() => mouseCleanupEvent.OnNext(Unit.Default));
                    }
                });

            var touchNotifications = notifications.OfType<TouchNotification>();

            var touchEvents = Observable.Merge(
                touchNotifications.CombineLatest(
                    viewport,
                    (n, v) => new TouchEventArgs(Notification: n, Viewport: v)
                ),
                onReset.Select(_ => new TouchEventArgs(IsReset: true)),
                touchCleanupEvent.Select(_ => new TouchEventArgs(IsCleanup: true))
            );

            var touchPointers = touchEvents
                .Scan(
                    ImmutableDictionary<int, FuPointer>.Empty,
                    (pointers, ev) =>
                    {
                        if (ev.IsCleanup)
                        {
                            var builder = pointers.ToBuilder();
                            foreach (var kvp in pointers)
                            {
                                if (kvp.Value.State == TouchNotificationKind.TouchUp)
                                    builder.Remove(kvp.Key);
                            }
                            return builder.ToImmutable();
                        }

                        if (ev.IsReset)
                        {
                            var builder = pointers.ToBuilder();
                            foreach (var kvp in pointers)
                            {
                                if (kvp.Value.State != TouchNotificationKind.TouchUp)
                                {
                                    builder[kvp.Key] = kvp.Value.WithState(
                                        TouchNotificationKind.TouchUp
                                    );
                                }
                            }
                            return builder.ToImmutable();
                        }

                        var (notification, viewport) = (ev.Notification, ev.Viewport);

                        if (notification is NotificationWithPosition notificationWithPosition)
                        {
                            // Projected position
                            var position = notificationWithPosition.ToCurrentSpace(viewport);

                            // Existing pointer
                            if (pointers.TryGetValue(notification.Id, out var pointer))
                            {
                                return pointers.SetItem(
                                    notification.Id,
                                    pointer.WithPosition(position).WithState(notification.Kind)
                                );
                            }
                            // New pointer
                            else
                            {
                                // Pointer does not exist and notification is move
                                // Create touchdown pointer
                                if (notification.Kind is TouchNotificationKind.TouchMove)
                                {
                                    return pointers.SetItem(
                                        notification.Id,
                                        new FuPointer(
                                            notification.Id,
                                            position,
                                            TouchNotificationKind.TouchDown
                                        )
                                    );
                                }

                                return pointers.SetItem(
                                    notification.Id,
                                    new FuPointer(notification.Id, position, notification.Kind)
                                );
                            }
                        }

                        return pointers;
                    }
                )
                .Do(pointers =>
                {
                    if (
                        pointers.Values.Any(pointer =>
                            pointer.State is TouchNotificationKind.TouchUp
                        )
                    )
                    {
                        Scheduler.Default.Schedule(() => touchCleanupEvent.OnNext(Unit.Default));
                    }
                });

            _activeStream = touchActive
                .Select(isTouchActive => isTouchActive ? touchPointers : mousePointers)
                .Switch()
                .Replay(1)
                .RefCount();
        }

        public IDisposable Subscribe(IObserver<IReadOnlyDictionary<int, FuPointer>> observer)
        {
            return _activeStream.Subscribe(observer);
        }
    }
}
