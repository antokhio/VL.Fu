using Stride.Core.Mathematics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Skia;

namespace VL.Fu.Services
{
    public class ViewService : InstancedId, IContextedService, INotifiable, IDisposable
    {
        public RectangleF ViewBounds { get; private set; } = RectangleF.Empty;
        public Int2 Resolution { get; private set; } = Int2.Zero;
        public CommonSpace Space { get; private set; } = Constants.DefaultCommonSpace;
        public float DIPFactor { get; private set; } = Constants.DefaultDIPFactor;
        public float InverseDIPFactor => 1.0f / DIPFactor;
        public float PixelFactor { get; private set; } = Constants.DefaultPixelFactor;
        public float InversePixelFactor => 1.0f / PixelFactor;
        public DipFactorMode DipFactorMode { get; private set; } = Constants.DefaultDipFactorMode;

        private readonly Subject<INotification> _notifications = new();
        private readonly IChannel<float> _scaling = new ChannelProperty<float>(
            Constants.DefaultScalling
        );
        protected readonly CompositeDisposable _subscriptions = new();

        public ViewService(Configuration configuration)
        {
            PixelFactor = configuration.PixelFactor;

            _subscriptions.Add(configuration.Space.Subscribe(space => Space = space));
            _subscriptions.Add(
                configuration.DIPFactor.Subscribe(dipFactor => DIPFactor = dipFactor)
            );
            _subscriptions.Add(
                configuration.DipFactorMode.Subscribe(dipFactorMode =>
                    DipFactorMode = dipFactorMode
                )
            );

            // Looks like dip factor should be only kept internally
            // and if user change dip factor shoud be multipled here
            // same as pixel factor?
            _subscriptions.Add(
                _scaling
                    .Where(_ => DipFactorMode is DipFactorMode.Scaling)
                    .Subscribe(scalling =>
                        configuration.DIPFactor.EnsureValue(
                            (int)(Constants.DefaultPixelFactor * scalling)
                        )
                    )
            );

            var clientAreaStream = _notifications
                .OfType<NotificationWithClientArea>()
                .Select(n => n.ClientArea)
                .DistinctUntilChanged();
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
