using System.Reactive;
using System.Reactive.Linq;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services.Input
{
    public class FocusHandler : IObservable<bool>
    {
        protected readonly IObservable<bool> _activeStream;

        public IObservable<Unit> OnFocusFound { get; }
        public IObservable<Unit> OnFocusLost { get; }

        public FocusHandler(IObservable<INotification> notifications)
        {
            var focusFoundNotifications = notifications
                .OfType<GotFocusNotification>()
                .Publish()
                .RefCount();
            var focusLostNotifications = notifications
                .OfType<LostFocusNotification>()
                .Publish()
                .RefCount();

            OnFocusFound = focusFoundNotifications.Select(_ => Unit.Default);
            OnFocusLost = focusLostNotifications.Select(_ => Unit.Default);

            var focusFoundStream = focusFoundNotifications.Select(_ => true);
            var focusLostStream = focusLostNotifications.Select(_ => false);

            _activeStream = Observable
                .Merge(focusLostStream, focusFoundStream)
                .StartWith(true)
                .DistinctUntilChanged()
                .Replay(1)
                .RefCount();
        }

        public IDisposable Subscribe(IObserver<bool> observer)
        {
            return _activeStream.Subscribe(observer);
        }
    }
}
