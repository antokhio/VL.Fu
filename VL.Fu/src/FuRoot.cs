using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Services;
using VL.Lib.IO.Notifications;
using VL.Skia;
using YogaSharp;

namespace VL.Fu
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit, HasStateOutput = true)]
    public class FuRoot : ViewportBoundsProvider, IContextProvider, ILayer
    {
        public RectangleF? Bounds => Root?.Bounds;

        private ViewportService _viewportService;

        // TODO: Remove from here
        private float _pointScaleFactor = 100.0f;
        private YGErrata _errata = YGErrata.None;
        private bool _useWebDefaults = false;
        private unsafe YGConfig* _handle = YGConfig.GetDefault();

        [Fragment]
        public FuRoot(NodeContext nodeContext)
            : base(nodeContext)
        {
            _viewportService = new ViewportService(this);
            var notificationsSerivce = new NotificationsService(this, _viewportService);
            var interactionService = new InteractionService(this, notificationsSerivce);

            RegisterService<IViewportService>(_viewportService);
            RegisterService<INotificationsService>(notificationsSerivce);
            RegisterService<IInteractionService>(interactionService);

            ConfigSetDefaults();
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
            return Root?.Notify(notification, caller) ?? false;
        }

        public unsafe void ConfigSetDefaults()
        {
            _handle->SetPointScaleFactor(_pointScaleFactor);
            _handle->SetErrata(_errata);
            _handle->SetUseWebDefaults(_useWebDefaults);
        }

        [Fragment]
        public override void Update()
        {
            base.Update();

            if (Root?.IsDirty() ?? false)
            {
                Root?.CalculateLayout(_viewportService.Viewport.ViewportBounds, YGDirection.LTR);
            }
        }
    }
}
