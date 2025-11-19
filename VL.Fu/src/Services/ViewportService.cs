using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Stride.Core.Mathematics;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Notfications;
using VL.Fu.Extensions;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Services
{
    public class ViewportService : InstancedId, IContextedService, INotifiable
    {
        public RectangleF ViewportBounds { get; private set; } = RectangleF.Empty;
        public Int2 Resolution { get; private set; } = Int2.Zero;
        public CommonSpace Space { get; private set; } = Constants.DefaultSpace;
        public int DIPFactor { get; private set; } = Constants.DefaultDIPFactor;
        public float InverseDIPFactor => 1.0f / DIPFactor;
        public int PixelFactor { get; private set; } = Constants.DefaultPixelFactor;
        public float InversePixelFactor => 1.0f / PixelFactor;
        public ScalingMode ScalingMode { get; private set; } = Constants.DefaultScalingMode;
        public float Scaling { get; private set; } = Constants.DefaultScaling;

        private readonly Subject<INotification> _notifications = new();
        private readonly CompositeDisposable _subscriptions = new();

        public ViewportService(Configuration configuration)
        {
            var clientAreaStream = _notifications
                .OfType<NotificationWithClientArea>()
                .Select(n => n.ClientArea)
                .DistinctUntilChanged();

            var viewportBoundsStream = _notifications
                .OfType<ViewportBoundsNotification>()
                .Select(n => n.ViewportBounds);

            var viewportScalingStream = _notifications
                .OfType<ViewportBoundsNotification>()
                .Select(n => n.Scaling);

            // Combine all configuration streams
            var factorsStream = Observable
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
                        new
                        {
                            PixelFactor = pixelFactor,
                            DIPFactor = dipFactor,
                            ScalingMode = scalingMode,
                            Scaling = scaling,
                            ClientArea = clientArea,
                            Space = space,
                            ViewportBounds = bounds,
                        }
                )
                .DistinctUntilChanged();

            // Produce state update
            _subscriptions.Add(
                factorsStream.Subscribe(state =>
                {
                    // Update properties in explicit order
                    ScalingMode = state.ScalingMode;
                    Scaling = state.Scaling;
                    Resolution = state.ClientArea.ToInt();

                    // Calculate factors with consistent state
                    PixelFactor = state.PixelFactor.WithPixelFactorScalingMode(
                        ScalingMode,
                        Scaling
                    );
                    DIPFactor = state.DIPFactor.WithDIPFactorScalingMode(ScalingMode, Scaling);

                    Space = state.Space;
                    ViewportBounds = state.ViewportBounds;
                })
            );
        }

        public void Notify(INotification notification)
        {
            _notifications.OnNext(notification);
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
            _notifications?.Dispose();
        }
    }
}
