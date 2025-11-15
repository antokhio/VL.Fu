using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Stride.Core.Mathematics;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
using VL.Fu.Extensions;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Skia;

namespace VL.Fu.Services
{
    public class ViewService : InstancedId, IContextedService, INotifiable, IDisposable
    {
        public RectangleF ViewBounds { get; private set; } = RectangleF.Empty;
        public Int2 Resolution { get; private set; } = Int2.Zero;
        public CommonSpace Space { get; private set; } = Constants.DefaultSpace;
        public float DIPFactor { get; private set; } = Constants.DefaultDIPFactor;
        public float InverseDIPFactor => 1.0f / DIPFactor;
        public float PixelFactor { get; private set; } = Constants.DefaultPixelFactor;
        public float InversePixelFactor => 1.0f / PixelFactor;
        public ScalingMode ScalingMode { get; private set; } = Constants.DefaultScalingMode;

        private readonly Subject<INotification> _notifications = new();
        private readonly IChannel<float> _scaling = new ChannelProperty<float>(
            Constants.DefaultScaling
        );
        public float Scaling { get; private set; } = Constants.DefaultScaling;

        protected readonly CompositeDisposable _subscriptions = new();

        public ViewService(Configuration configuration)
        {
            _subscriptions.Add(configuration.Space.Subscribe(space => Space = space));

            _subscriptions.Add(
                configuration.PixelFactor.Subscribe(pixelFactor =>
                    PixelFactor = ScalingMode.ToPixelFactor(pixelFactor, Scaling)
                )
            );
            _subscriptions.Add(configuration.Space.Subscribe(space => Space = space));
            _subscriptions.Add(
                configuration.DIPFactor.Subscribe(dipFactor =>
                    DIPFactor = ScalingMode.ToDIPFactor(dipFactor, Scaling)
                )
            );
            _subscriptions.Add(
                configuration.ScalingMode.Subscribe(scalingMode =>
                {
                    ScalingMode = scalingMode;
                    PixelFactor = ScalingMode.ToPixelFactor(
                        configuration.PixelFactor.Value,
                        Scaling
                    );
                    DIPFactor = ScalingMode.ToDIPFactor(configuration.DIPFactor.Value, Scaling);
                })
            );

            _subscriptions.Add(
                _scaling.Subscribe(scaling =>
                {
                    Scaling = scaling;
                    PixelFactor = ScalingMode.ToPixelFactor(
                        configuration.PixelFactor.Value,
                        Scaling
                    );
                    DIPFactor = ScalingMode.ToDIPFactor(configuration.DIPFactor.Value, Scaling);
                })
            );

            var clientAreaStream = _notifications
                .OfType<NotificationWithClientArea>()
                .Select(n => n.ClientArea)
                .DistinctUntilChanged();

            _subscriptions.Add(
                clientAreaStream.Subscribe(clientArea =>
                {
                    // 3. When the trigger fires, use the *current values* of the properties.
                    Resolution = new Int2((int)clientArea.X, (int)clientArea.Y);
                    var boundsInPixels = new RectangleF(0, 0, clientArea.X, clientArea.Y);

                    // Use the class properties which are kept up-to-date by their own subscriptions.
                    ViewBounds = Space switch
                    {
                        CommonSpace.Normalized => boundsInPixels.ToNormalizedSpace(Resolution),
                        CommonSpace.DIP => boundsInPixels.ToCenteredDIPSpace(Resolution, DIPFactor),
                        CommonSpace.DIPTopLeft => boundsInPixels.ToDIPTopLeftSpace(DIPFactor),
                        CommonSpace.PixelTopLeft => boundsInPixels.ToPixelTopLeftSpace(PixelFactor),
                        _ => RectangleF.Empty,
                    };
                })
            );
        }

        public void Notify(INotification notification, CallerInfo caller)
        {
            _notifications.OnNext(notification);
            _scaling.EnsureValue(caller.Scaling);
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
    }
}
