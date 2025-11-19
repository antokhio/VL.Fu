using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services
{
    public class InputService : IContextedService, INotifiable
    {
        public Subject<FuInputState> InputStateStream { get; }
        public IObservable<bool> IsTouchActiveStream { get; }

        private readonly Subject<INotification> _notifications = new();
        private readonly CompositeDisposable _subscriptions = new();

        private record CleanupPointer(int Id);

        public InputService(Configuration configuration, ViewportService viewportService)
        {
            var enabledNotifications = _notifications
                .Where(_ => configuration.Enabled.Value)
                .Publish()
                .RefCount();

            var touchNotifications = enabledNotifications.OfType<TouchNotification>();

            IsTouchActiveStream = touchNotifications
                .Select(_ => true)
                .Merge(
                    touchNotifications.Throttle(TimeSpan.FromMilliseconds(150)).Select(_ => false)
                )
                .StartWith(false)
                .Replay(1)
                .RefCount();

            //var pointerEvents = touchNotifications
            //    .SelectMany(n =>
            //    {
            //        var pointer = n.ToPointer(viewportService.Viewport);

            //        if (n.Kind == TouchNotificationKind.TouchUp)
            //        {
            //            // For a TouchUp, create two events: the TouchUp itself, and a delayed cleanup event.
            //            return new[]
            //            {
            //                Observable.Return((object)pointer),
            //                Observable
            //                    .Return((object)new CleanupPointer(pointer.Id))
            //                    .Delay(TimeSpan.FromMilliseconds(16)),
            //            };
            //        }
            //        return new[] { Observable.Return((object)pointer) };
            //    })
            //    .Merge();
        }

        public void Notify(INotification notification) => _notifications.OnNext(notification);

        public void Dispose()
        {
            _notifications.OnCompleted();
            _subscriptions.Dispose();
        }
    }
}
