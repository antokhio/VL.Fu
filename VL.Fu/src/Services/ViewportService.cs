using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Stride.Core.Mathematics;
using VL.Fu.Core;
using VL.Fu.Core.Notifications;
using VL.Fu.Core.Repository;
using VL.Fu.Extensions;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Services
{
    /// <summary>
    /// A reactive service that calculates the viewport bounds in various coordinate spaces based on notifications.
    /// </summary>
    public class ViewportService : IRepositoryService, INotifiable
    {
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Subject<INotification> _notifications = new();

        /// <summary>
        /// The calculated bounds of the viewport in the current coordinate space.
        /// </summary>
        public RectangleF Bounds { get; private set; } = RectangleF.Empty;

        /// <summary>
        /// The last known resolution of the viewport in pixels.
        /// </summary>
        public Int2 Resolution { get; private set; } = Int2.Zero;

        public ViewportService(ConfigurationBase configuration)
        {
            // Stream of client area notifications
            var clientAreaStream = _notifications
                .OfType<NotificationWithClientArea>()
                .Select(n => n.ClientArea)
                .DistinctUntilChanged();

            // We use CombineLatest because we need to recalculate the bounds if EITHER the
            // client area changes OR any of the configuration settings change.
            var combinedStream = clientAreaStream.CombineLatest(
                configuration.Space,
                configuration.DIPFactor,
                (clientArea, space, dip) => (clientArea, space, dip, configuration.PixelFactor)
            );

            _subscriptions.Add(
                combinedStream.Subscribe(t =>
                {
                    var (clientArea, space, dipFactor, pixelFactor) = t;

                    Resolution = new Int2((int)clientArea.X, (int)clientArea.Y);
                    var boundsInPixels = new RectangleF(0, 0, clientArea.X, clientArea.Y);

                    Bounds = space switch
                    {
                        CommonSpace.Normalized => boundsInPixels.ToNormalizedSpace(Resolution),
                        CommonSpace.DIP => boundsInPixels.ToCenteredDIPSpace(Resolution, dipFactor),
                        CommonSpace.DIPTopLeft => boundsInPixels.ToDIPTopLeftSpace(dipFactor),
                        CommonSpace.PixelTopLeft => boundsInPixels.ToPixelTopLeftSpace(pixelFactor),
                        _ => RectangleF.Empty,
                    };
                })
            );
        }

        /// <summary>
        /// Pushes a raw notification into the service for processing.
        /// </summary>
        public void Notify(INotification notification) => _notifications.OnNext(notification);

        public void Dispose()
        {
            _subscriptions.Dispose();
            _notifications.Dispose();
        }
    }
}
