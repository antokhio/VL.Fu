using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core;
using VL.Fu.Extensions;
using VL.Fu.Helpers;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Skia;

namespace VL.Fu.Services
{
    public class InputService : IDisposable
    {
        public Spread<FuCursor> Cursors => _cursorsChannel.Value;
        public FuMouse Mouse => _mouse.Value;
        public bool HasTouch { get; private set; }
        public bool IsEnabled { get; set; } = true;

        protected readonly Subject<INotification> _notifications = new Subject<INotification>();
        protected readonly IChannel<FuMouse> _mouse = ChannelHelpers.CreateChannelOfType<FuMouse>();
        protected FuMouse? _previousMouse = null;

        protected readonly CompositeDisposable _subscriptions = new();

        protected readonly Dictionary<int, FuCursor> _cursors = new();

        protected readonly List<int> _cursorsToRemove = new();

        protected IChannel<Spread<FuCursor>> _cursorsChannel = ChannelHelpers.CreateChannelOfType<
            Spread<FuCursor>
        >();

        public InputService()
        {
            _cursorsChannel.Value = Spread<FuCursor>.Empty;

            // Pre-process mouse
            _subscriptions.Add(
                _notifications
                    .OfType<MouseNotification>()
                    .Subscribe(n =>
                        _mouse.OnNext(
                            n switch
                            {
                                MouseDownNotification mdn => Mouse.With(mdn),
                                MouseUpNotification mun => Mouse.With(mun),
                                MouseMoveNotification mmn => Mouse.With(mmn),
                                MouseWheelNotification mwn => Mouse.With(mwn),
                                MouseLostNotification mwl => Mouse.With(mwl),
                                _ => Mouse,
                            }
                        )
                    )
            );

            // Process touch
            _subscriptions.Add(
                _notifications
                    .OfType<TouchNotification>()
                    .Subscribe(x =>
                    {
                        if (x.Kind == TouchNotificationKind.TouchDown)
                        {
                            var cursor = x.ToNewFuCursor();
                            _cursors[cursor.Id] = cursor;
                        }
                        else if (x.Kind == TouchNotificationKind.TouchMove)
                        {
                            if (_cursors.TryGetValue(x.Id, out var prev))
                            {
                                var cursor = x.ToFuCursorWithDelta(prev);
                                _cursors[cursor.Id] = cursor;
                            }
                            else
                            {
                                var cursor = x.ToNewFuCursor();
                                _cursors[cursor.Id] = cursor;
                            }
                        }
                        else if (x.Kind == TouchNotificationKind.TouchUp)
                        {
                            if (_cursors.TryGetValue(x.Id, out var prev))
                            {
                                var cursor = x.ToFuCursorWithDelta(prev);
                                _cursors[cursor.Id] = cursor;

                                _cursorsToRemove.Add(cursor.Id);
                            }
                            else
                            {
                                var cursor = x.ToNewFuCursor();
                                _cursors[cursor.Id] = cursor;

                                _cursorsToRemove.Add(cursor.Id);
                            }
                        }

                        _cursorsChannel.OnNext(_cursors.Values.ToSpread());
                    })
            );

            // Process mouse
            _subscriptions.Add(
                _mouse.Subscribe(next =>
                {
                    // TODO: TOUCH
                    if (_previousMouse is not null)
                    {
                        var prev = _previousMouse.Value;

                        if (prev.IsLeftButton is false && next.IsLeftButton is true)
                        {
                            _cursors[MouseHelper.MouseCursorId] = next.ToNewFuCursor(
                                TouchNotificationKind.TouchDown
                            );
                        }
                        else if (prev.IsLeftButton is true && next.IsLeftButton is true)
                        {
                            if (_cursors.TryGetValue(MouseHelper.MouseCursorId, out var prevCursor))
                            {
                                _cursors[MouseHelper.MouseCursorId] = next.ToFuCursorWithDelta(
                                    prevCursor,
                                    TouchNotificationKind.TouchMove
                                );
                            }
                            else
                            {
                                _cursors[MouseHelper.MouseCursorId] = next.ToNewFuCursor(
                                    TouchNotificationKind.TouchMove
                                );
                            }
                        }
                        else if (prev.IsLeftButton is true && next.IsLeftButton is false)
                        {
                            if (_cursors.TryGetValue(MouseHelper.MouseCursorId, out var prevCursor))
                            {
                                _cursors[MouseHelper.MouseCursorId] = next.ToFuCursorWithDelta(
                                    prevCursor,
                                    TouchNotificationKind.TouchUp
                                );
                            }
                            else
                            {
                                _cursors[MouseHelper.MouseCursorId] = next.ToNewFuCursor(
                                    TouchNotificationKind.TouchUp
                                );
                            }

                            _cursorsToRemove.Add(MouseHelper.MouseCursorId);
                        }

                        _previousMouse = next;
                    }
                    else if (next.IsLeftButton is true)
                    {
                        _cursors[MouseHelper.MouseCursorId] = next.ToNewFuCursor(
                            TouchNotificationKind.TouchDown
                        );

                        _previousMouse = next;
                    }

                    _cursorsChannel.OnNext(_cursors.Values.ToSpread());
                })
            );
        }

        public void Update()
        {
            if (_cursorsToRemove.Count() > 0)
            {
                foreach (var cursorId in _cursorsToRemove)
                {
                    _cursors.Remove(cursorId);
                }

                _cursorsToRemove.Clear();

                _cursorsChannel.OnNext(_cursors.Values.ToSpread());
            }
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            if (IsEnabled)
                _notifications.OnNext(notification);

            return false;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
