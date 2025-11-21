using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Context;
using VL.Fu.Core.Services;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit, HasStateOutput = true)]
    public class FuRoot : ViewportBoundsProvider, IContextProvider, ILayer
    {
        public RectangleF? Bounds => Root?.Bounds;

        [Fragment]
        public FuRoot(NodeContext nodeContext)
            : base(nodeContext)
        {
            var viewPortService = new ViewportService(this);
            var notificationsSerivce = new NotificationsService(this, viewPortService);

            RegisterService<IViewportService>(viewPortService);
            RegisterService<INotificationsService>(notificationsSerivce);
        }

        [Fragment(Order = PinOrder.Input)]
        public void SetInput(IFuNode? input) => Root = input;

        public void Render(CallerInfo caller)
        {
            base.Render(caller);
            Root?.Render(caller);
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            BroadcastNotification(notification);

            Console.WriteLine($"{notification.GetType().ToString()} {caller.GetHashCode()}");

            return Root?.Notify(notification, caller) ?? false;
        }
    }
}
