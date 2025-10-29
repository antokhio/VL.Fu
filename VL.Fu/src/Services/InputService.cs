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
        public Spread<Cursor> Cursors => _cursorsChannel.Value;
        public Mouse Mouse => _mouse.Value;
        public bool HasTouch { get; private set; }
        public bool IsEnabled { get; set; } = true;

        protected readonly Subject<INotification> _notifications = new Subject<INotification>();
        protected readonly IChannel<Mouse> _mouse = ChannelHelpers.CreateChannelOfType<Mouse>();
        protected Mouse? _previousMouse = null;

        protected readonly CompositeDisposable _subscriptions = new();

        protected readonly Dictionary<int, Cursor> _cursors = new();

        protected readonly List<int> _cursorsToRemove = new();

        protected IChannel<Spread<Cursor>> _cursorsChannel = ChannelHelpers.CreateChannelOfType<
            Spread<Cursor>
        >();

        public InputService()
        {
            _cursorsChannel.Value = Spread<Cursor>.Empty;

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
                        var cursor = x.ToCursor();

                        _cursors[cursor.Id] = cursor;

                        if (x.Kind == TouchNotificationKind.TouchUp)
                        {
                            _cursorsToRemove.Add(cursor.Id);
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
                            _cursors[MouseHelpers.MouseCursorId] = next.ToCursor(
                                TouchNotificationKind.TouchDown
                            );
                        }
                        else if (prev.IsLeftButton is true && next.IsLeftButton is true)
                        {
                            _cursors[MouseHelpers.MouseCursorId] = next.ToCursor(
                                TouchNotificationKind.TouchMove
                            );
                        }
                        else if (prev.IsLeftButton is true && next.IsLeftButton is false)
                        {
                            _cursors[MouseHelpers.MouseCursorId] = next.ToCursor(
                                TouchNotificationKind.TouchUp
                            );

                            _cursorsToRemove.Add(MouseHelpers.MouseCursorId);
                        }

                        _previousMouse = next;
                    }
                    else if (next.IsLeftButton is true)
                    {
                        _cursors[MouseHelpers.MouseCursorId] = next.ToCursor(
                            TouchNotificationKind.TouchDown
                        );

                        _previousMouse = next;
                    }
                    else if (next.IsLost)
                    {
                        _cursors[MouseHelpers.MouseCursorId] = next.ToCursor(
                            TouchNotificationKind.TouchUp
                        );

                        _previousMouse = null;

                        _cursorsToRemove.Add(MouseHelpers.MouseCursorId);
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
            throw new NotImplementedException();
        }
    }
}
