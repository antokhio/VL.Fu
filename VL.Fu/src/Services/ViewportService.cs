using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Stride.Core.Mathematics;
using VL.Fu.Core;
using VL.Fu.Core.Common;
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

        public CommonSpace Space { get; private set; } = Constants.DefaultCommonSpace;
        public float DIPFactor { get; private set; } = Constants.DefaultDIPFactor;
        public float PixelFactor { get; private set; } = Constants.DefaultPixelFactor;

        public ViewportService(ConfigurationBase configuration)
        {
            // Set the constant value
            PixelFactor = configuration.PixelFactor;

            // Subscribe to configuration changes to keep our local properties up-to-date.
            _subscriptions.Add(configuration.Space.Subscribe(s => Space = s));
            _subscriptions.Add(configuration.DIPFactor.Subscribe(f => DIPFactor = f));

            // --- The Corrected Logic ---
            // 1. Define our trigger: the stream of client area notifications.
            var clientAreaStream = _notifications
                .OfType<NotificationWithClientArea>()
                .Select(n => n.ClientArea)
                .DistinctUntilChanged();

            // 2. Subscribe directly to the trigger.
            _subscriptions.Add(
                clientAreaStream.Subscribe(clientArea =>
                {
                    // 3. When the trigger fires, use the *current values* of the properties.
                    Resolution = new Int2((int)clientArea.X, (int)clientArea.Y);
                    var boundsInPixels = new RectangleF(0, 0, clientArea.X, clientArea.Y);

                    // Use the class properties which are kept up-to-date by their own subscriptions.
                    Bounds = Space switch
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
