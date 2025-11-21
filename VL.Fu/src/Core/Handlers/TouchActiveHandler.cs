using System.Reactive;
using System.Reactive.Linq;
using VL.Fu.Core.Common;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Handlers
{
    public class TouchActiveHandler : IObservable<bool>
    {
        private readonly IObservable<bool> _activeStream;

        public TouchActiveHandler(
            IObservable<INotification> notifications,
            IObservable<Unit> resetSignal,
            TimeSpan? activityTimeout = null
        )
        {
            var touchActivityTimeout = activityTimeout ?? Constants.DefaultTouchActivityTimeout;
            var touchNotifications = notifications.OfType<TouchNotification>();

            var onActive = touchNotifications.Select(_ => true);
            var onInactive = touchNotifications.Throttle(touchActivityTimeout).Select(_ => false);

            var onReset = resetSignal.Select(_ => false);

            _activeStream = Observable
                .Merge(onActive, onInactive, onReset)
                .StartWith(false)
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
