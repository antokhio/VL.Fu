using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Services;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu
{
    [ProcessNode(
        Name = "Fu",
        HasStateOutput = true,
        FragmentSelection = FragmentSelection.Explicit
    )]
    public class Fu : ILayer
    {
        public InputService InputService { get; } = new();

        public void RegisterServices(AppHost appHost)
        {
            appHost.Services.RegisterService(InputService);
        }

        [Fragment]
        public Fu(AppHost appHost)
        {
            RegisterServices(appHost);
        }

        public RectangleF? Bounds => RectangleF.Empty;

        public bool Notify(INotification notification, CallerInfo caller) =>
            InputService.Notify(notification, caller);

        private bool invalidate = true;

        public void Render(CallerInfo caller) { }

        [Fragment]
        public void Update()
        {
            InputService.Update();
        }
    }
}
