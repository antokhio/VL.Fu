using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Services.Input;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services
{
    public class NotificationsService : InstancedId, IContextedService, INotifiable
    {
        public IObservable<bool> IsEnabled => _enabledHandler;
        public IObservable<bool> IsFocused => _focusHandler;
        public IObservable<Unit> OnFocusFound => _focusHandler.OnFocusFound;
        public IObservable<Unit> OnFocusLost => _focusHandler.OnFocusLost;
        public IObservable<Unit> OnReset => _resetHandler;
        public IObservable<FuMouse> Mouse => _mouseHandler;
        public IObservable<IReadOnlyDictionary<int, FuPointer>> Pointers { get; }
        public IObservable<IReadOnlySet<FuKey>> Keys => _keysHandler;
        public IObservable<bool> IsTouchActive => _touchActiveHandler;
        public IObservable<bool> IsInputActive { get; }

        private readonly FocusHandler _focusHandler;
        private readonly EnabledHandler _enabledHandler;
        private readonly ResetHandler _resetHandler;
        private readonly TouchActiveHandler _touchActiveHandler;
        private readonly MouseHandler _mouseHandler;
        private readonly KeysHandler _keysHandler;
        private readonly PointersHandler _pointersHandler;

        private readonly IObservable<FuViewport> _viewport;

        private readonly Subject<INotification> _notifications = new();
        private readonly CompositeDisposable _subscriptions = new();

        public NotificationsService(Configuration configuration, IViewportService viewportService)
        {
            _viewport = viewportService.ViewportStream;

            _focusHandler = new FocusHandler(_notifications);
            _enabledHandler = new EnabledHandler(configuration);
            _resetHandler = new ResetHandler(_enabledHandler, _focusHandler);

            var enablledNotifications = _enabledHandler.Select(IsEnabled =>
                IsEnabled ? _notifications : Observable.Empty<INotification>()
            );

            _mouseHandler = new MouseHandler(_notifications, _viewport, _resetHandler);
            _keysHandler = new KeysHandler(_notifications, _resetHandler);

            _touchActiveHandler = new TouchActiveHandler(_notifications, OnReset);

            // TODO:
            _pointersHandler = new PointersHandler(
                _notifications,
                _mouseHandler,
                _touchActiveHandler,
                _resetHandler
            );
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Notify(INotification notification)
        {
            _notifications.OnNext(notification);
        }
    }
}
