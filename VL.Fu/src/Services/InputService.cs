using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core;
using VL.Fu.Extensions;
using VL.Fu.Helpers;
using VL.Lib.Collections;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Skia;

namespace VL.Fu.Services
{
    public class InputService : IDisposable
    {
        public int DIPFactor => _instance.DIPFactor;
        public int PixelFactor => _instance.PixelFactor;
        public CommonSpace Space => _instance.Space;
        public FuMouse Mouse => _mouse.Value;
        public Spread<FuCursor> Cursors => _cursorsChannel.Value!;
        public Spread<FuKey> Keys => _keysChannel.Value!;
        public Spread<FuKey> Modifiers => _modifiersChannel.Value!;

        public bool IsFocused { get; private set; }
        public bool IsTouch { get; private set; }
        public bool IsEnabled { get; set; } = true;

        protected readonly Subject<INotification> _notifications = new Subject<INotification>();
        protected readonly IChannel<FuMouse> _mouse = ChannelHelpers.CreateChannelOfType<FuMouse>();

        protected FuMouse? _previousMouse = null;

        protected readonly CompositeDisposable _subscriptions = new();

        protected readonly Dictionary<int, FuCursor> _cursors = new();
        protected readonly Dictionary<Keys, FuKey> _keys = new();

        protected readonly List<int> _cursorsToRemove = new();

        protected readonly IChannel<Spread<FuCursor>> _cursorsChannel =
            ChannelHelpers.CreateChannelOfType<Spread<FuCursor>>();

        protected readonly IChannel<Spread<FuKey>> _keysChannel =
            ChannelHelpers.CreateChannelOfType<Spread<FuKey>>();

        protected readonly IChannel<Spread<FuKey>> _modifiersChannel =
            ChannelHelpers.CreateChannelOfType<Spread<FuKey>>();

        private readonly Fu _instance;

        public InputService(Fu instance)
        {
            _instance = instance;

            _cursorsChannel.Value = Spread<FuCursor>.Empty;
            _keysChannel.Value = Spread<FuKey>.Empty;
            _modifiersChannel.Value = Spread<FuKey>.Empty;

            // Pre-process mouse
            _subscriptions.Add(
                _notifications
                    .OfType<MouseNotification>()
                    .Subscribe(n =>
                        _mouse.OnNext(
                            n switch
                            {
                                MouseDownNotification mdn => Mouse.With(
                                    mdn,
                                    Space,
                                    DIPFactor,
                                    PixelFactor
                                ),
                                MouseUpNotification mun => Mouse.With(
                                    mun,
                                    Space,
                                    DIPFactor,
                                    PixelFactor
                                ),
                                MouseMoveNotification mmn => Mouse.With(
                                    mmn,
                                    Space,
                                    DIPFactor,
                                    PixelFactor
                                ),
                                MouseWheelNotification mwn => Mouse.With(
                                    mwn,
                                    Space,
                                    DIPFactor,
                                    PixelFactor
                                ),
                                MouseLostNotification mwl => Mouse.With(mwl),
                                _ => Mouse,
                            }
                        )
                    )
            );

            // Process keyboard
            _subscriptions.Add(
                _notifications
                    .OfType<KeyDownNotification>()
                    .Subscribe(n =>
                    {
                        _keys[n.KeyCode] = new FuKey(n.KeyCode, KeyNotificationKind.KeyDown);

                        if (_keys[n.KeyCode].IsModifier())
                        {
                            _modifiersChannel.OnNext(
                                _keys.Values.Where(k => k.IsModifier()).ToSpread()
                            );
                        }
                        else
                        {
                            _keysChannel.OnNext(
                                _keys.Values.Where(k => !k.IsModifier()).ToSpread()
                            );
                        }
                    })
            );
            _subscriptions.Add(
                _notifications
                    .OfType<KeyUpNotification>()
                    .Subscribe(n =>
                    {
                        bool isModifier = false;

                        if (_keys.TryGetValue(n.KeyCode, out var fuKey))
                        {
                            isModifier = fuKey.IsModifier();
                            _keys.Remove(n.KeyCode);
                        }

                        if (isModifier)
                        {
                            _modifiersChannel.OnNext(
                                _keys.Values.Where(k => k.IsModifier()).ToSpread()
                            );
                        }
                        else
                        {
                            _keysChannel.OnNext(
                                _keys.Values.Where(k => !k.IsModifier()).ToSpread()
                            );
                        }
                    })
            );

            // Focus lost
            _subscriptions.Add(
                _notifications
                    .OfType<LostFocusNotification>()
                    .Subscribe(x =>
                    {
                        IsFocused = false;

                        _keys.Clear();
                        _keysChannel.OnNext(Spread<FuKey>.Empty);
                        _modifiersChannel.OnNext(Spread<FuKey>.Empty);
                    })
            );

            // Focus Found
            _subscriptions.Add(
                _notifications
                    .OfType<GotFocusNotification>()
                    .Subscribe(x =>
                    {
                        IsFocused = true;
                    })
            );

            // Process touch
            _subscriptions.Add(
                _notifications
                    .OfType<TouchNotification>()
                    .Subscribe(x =>
                    {
                        IsTouch = true;

                        if (x.Kind == TouchNotificationKind.TouchDown)
                        {
                            var cursor = x.ToNewFuCursor(Space, DIPFactor, PixelFactor);
                            _cursors[cursor.Id] = cursor;
                        }
                        else if (x.Kind == TouchNotificationKind.TouchMove)
                        {
                            if (_cursors.TryGetValue(x.Id, out var prev))
                            {
                                var cursor = x.ToFuCursorWithDelta(
                                    prev,
                                    Space,
                                    DIPFactor,
                                    PixelFactor
                                );
                                _cursors[cursor.Id] = cursor;
                            }
                            else
                            {
                                var cursor = x.ToNewFuCursor(Space, DIPFactor, PixelFactor);
                                _cursors[cursor.Id] = cursor;
                            }
                        }
                        else if (x.Kind == TouchNotificationKind.TouchUp)
                        {
                            if (_cursors.TryGetValue(x.Id, out var prev))
                            {
                                var cursor = x.ToFuCursorWithDelta(
                                    prev,
                                    Space,
                                    DIPFactor,
                                    PixelFactor
                                );
                                _cursors[cursor.Id] = cursor;

                                _cursorsToRemove.Add(cursor.Id);
                            }
                            else
                            {
                                var cursor = x.ToNewFuCursor(Space, DIPFactor, PixelFactor);
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
                    // else if (mouse lost)

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
