using Stride.Core.Mathematics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Notfications;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services
{
    public class ViewportService : InstancedId, IContextedService, INotifiable
    {
        public FuViewport Viewport { get; private set; }

        private readonly Subject<INotification> _notifications = new();
        private readonly CompositeDisposable _subscriptions = new();

        public ViewportService(Configuration configuration)
        {
            var clientAreaStream = _notifications
                .OfType<NotificationWithClientArea>()
                .Select(n => n.ClientArea)
                .DistinctUntilChanged();

            var viewportNotificationStream = _notifications
                .OfType<ViewportBoundsNotification>()
                .Publish()
                .RefCount();

            var viewportBoundsStream = viewportNotificationStream.Select(n => n.ViewportBounds);
            var viewportScalingStream = viewportNotificationStream.Select(n => n.Scaling);

            // Combine all configuration streams
            var streams = Observable
                .CombineLatest(
                    // Factors
                    configuration.PixelFactor.StartWith(configuration.PixelFactor.Value),
                    configuration.DIPFactor.StartWith(configuration.DIPFactor.Value),
                    // Scaling
                    configuration.ScalingMode.StartWith(configuration.ScalingMode.Value),
                    viewportScalingStream.StartWith(Constants.DefaultScaling),
                    // Space
                    clientAreaStream.StartWith(Vector2.Zero),
                    configuration.Space.StartWith(Constants.DefaultSpace),
                    // ViewBounds
                    viewportBoundsStream.StartWith(RectangleF.Empty),
                    (pixelFactor, dipFactor, scalingMode, scaling, clientArea, space, bounds) =>
                        new FuViewport(
                            pixelFactor,
                            dipFactor,
                            scalingMode,
                            scaling,
                            clientArea,
                            space,
                            bounds
                        )
                )
                .DistinctUntilChanged();

            // Produce state update
            _subscriptions.Add(
                streams.Subscribe(state =>
                {
                    Viewport = state;
                })
            );
        }

        public void Notify(INotification notification)
        {
            _notifications.OnNext(notification);
        }

        public void Dispose()
        {
            _notifications?.OnCompleted();
            _subscriptions?.Dispose();
        }
    }
}
