using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Repository;
using VL.Fu.Services;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit, HasStateOutput = true)]
    public class FuRoot : NotifiableBase, ILayer
    {
        private IFuNode? _inputNode;
        public RectangleF? Bounds => (_inputNode as ILayer)?.Bounds;

        [Fragment]
        public FuRoot()
        {
            var notificationService = new NotificationService(this);
            var viewportService = new ViewportService(this);
            var interactionService = new InteractionService();

            interactionService.Initialize(notificationService);

            Register(notificationService);
            Register(viewportService);
            Register(interactionService);
        }

        [Fragment(Order = PinOrder.Input)]
        public void Update(IFuNode input)
        {
            _inputNode = input;

            if (_inputNode is IRepositoryConsumer consumer)
            {
                consumer.SetContextId(this.InstanceId);
            }
        }

        public void Render(CallerInfo caller)
        {
            (_inputNode as ILayer)?.Render(caller);
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            BroadcastNotification(notification);
            return (_inputNode as ILayer)?.Notify(notification, caller) ?? false;
        }
    }
}
