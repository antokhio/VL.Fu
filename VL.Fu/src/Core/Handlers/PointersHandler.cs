using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Stride.Core.Mathematics;
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
                            // Filter out completely uninitialized ghost input (0,0 with no buttons)
                            // This prevents a "Hover" at (0,0) on startup if the mouse isn't actually there.
                            if (
                                ev.State.Position == Vector2.Zero
                                && ev.State.Wheel == Int2.Zero
                                && !ev.State.IsLeft
                                && !ev.State.IsRight
                                && !ev.State.IsMiddle
                            )
                            {
                                return pointers;
                            }

                            // CRITICAL FIX: Determine initial state from buttons.
                            // Previously, this forced 'TouchDown', causing an instant click on first move.
                            // Now we check if any button is actually pressed.
                            var isAnyButtonDown =
                                ev.State.IsLeft
                                || ev.State.IsRight
                                || ev.State.IsMiddle
                                || ev.State.IsXButton1
                                || ev.State.IsXButton2;
                            var initialKind = isAnyButtonDown
                                ? TouchNotificationKind.TouchDown
                                : TouchNotificationKind.TouchMove;

                            pointer = FuPointer.DefaultMousePointer.WithMouseState(
                                ev.State,
                                initialKind
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

            // Touch Logic (Unchanged)
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
                            var position = notificationWithPosition.ToCurrentSpace(viewport);

                            if (pointers.TryGetValue(notification.Id, out var pointer))
                            {
                                return pointers.SetItem(
                                    notification.Id,
                                    pointer.WithPosition(position).WithState(notification.Kind)
                                );
                            }
                            else
                            {
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
