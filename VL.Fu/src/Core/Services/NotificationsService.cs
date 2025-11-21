using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core.Handlers;
using VL.Fu.Core.Input;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Services
{
    public class NotificationsService : InstancedId, INotificationsService
    {
        public IObservable<bool> EnabledStream => _enabledHandler;
        public IObservable<bool> FocusedStream => _focusHandler;
        public IObservable<Unit> OnFocusFound => _focusHandler.OnFocusFound;
        public IObservable<Unit> OnFocusLost => _focusHandler.OnFocusLost;
        public IObservable<Unit> OnReset => _resetHandler;
        public IObservable<FuMouse> MouseStream => _mouseHandler;
        public IObservable<IReadOnlyDictionary<int, FuPointer>> PointersStream => _pointersHandler;
        public IObservable<IReadOnlySet<FuKey>> KeysStream => _keysHandler;
        public IObservable<bool> TouchActiveStream => _touchActiveHandler;
        public IObservable<bool> IsActiveStream => _activityHandler;

        private readonly FocusHandler _focusHandler;
        private readonly EnabledHandler _enabledHandler;
        private readonly ResetHandler _resetHandler;
        private readonly TouchActiveHandler _touchActiveHandler;
        private readonly MouseHandler _mouseHandler;
        private readonly KeysHandler _keysHandler;
        private readonly PointersHandler _pointersHandler;
        private readonly ActivityHandler _activityHandler;

        private readonly IObservable<FuViewport> _viewport;

        private readonly Subject<INotification> _notifications = new();

        public NotificationsService(Configuration configuration, IViewportService viewportService)
        {
            _viewport = viewportService.ViewportStream;

            _focusHandler = new FocusHandler(_notifications);
            _enabledHandler = new EnabledHandler(configuration);
            _resetHandler = new ResetHandler(_enabledHandler, _focusHandler);

            var enabledAndFocused = _enabledHandler.CombineLatest(
                _focusHandler,
                (enabled, focused) => enabled && focused
            );

            var enabledNotifications = enabledAndFocused
                .Select(enabledAndFocused =>
                    enabledAndFocused ? _notifications : Observable.Empty<INotification>()
                )
                .Switch();

            _mouseHandler = new MouseHandler(enabledNotifications, _viewport, _resetHandler);
            _keysHandler = new KeysHandler(enabledNotifications, _resetHandler);

            _touchActiveHandler = new TouchActiveHandler(enabledNotifications, OnReset);

            _pointersHandler = new PointersHandler(
                enabledNotifications,
                _mouseHandler,
                _touchActiveHandler,
                _viewport,
                _resetHandler
            );

            _activityHandler = new ActivityHandler(_mouseHandler, _keysHandler, _pointersHandler);
        }

        public void Dispose()
        {
            _notifications.Dispose();
        }

        public void Notify(INotification notification)
        {
            _notifications.OnNext(notification);
        }
    }
}
