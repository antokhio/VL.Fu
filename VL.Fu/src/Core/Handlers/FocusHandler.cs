using System.Reactive;
using System.Reactive.Linq;
using VL.Fu.Core.Common;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Handlers
{
    public class FocusHandler : IObservable<bool>
    {
        protected readonly IObservable<bool> _activeStream;

        public IObservable<Unit> OnFocusFound { get; }
        public IObservable<Unit> OnFocusLost { get; }

        public FocusHandler(IObservable<INotification> notifications, TimeSpan? gracePeriod = null)
        {
            var focusTimeout = gracePeriod ?? Constants.DefaultFocusGracePeriod;

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
            // Treat active touch as valid focus (prevents flicker when dragging)
            var touchActivityStream = notifications.OfType<TouchNotification>().Select(_ => true);

            var focusLostStream = focusLostNotifications.Select(_ => false);

            _activeStream = Observable
                .Merge(focusLostStream, focusFoundStream, touchActivityStream)
                .Select(hasFocus =>
                    hasFocus
                        ? Observable.Return(true)
                        // If false, wait grace period
                        : Observable.Return(false).Delay(focusTimeout)
                )
                .Switch() // If a new event comes during the Delay, the old one is cancelled
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
