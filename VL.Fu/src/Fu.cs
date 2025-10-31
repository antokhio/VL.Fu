using Microsoft.Extensions.DependencyInjection;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Helpers;
using VL.Fu.Repository;
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
    public class Fu : ILayer, IDisposable
    {
        private static int CallerHashDefault = -1;

        public InputService InputService { get; }
        public ViewportService ViewportService { get; }

        public int DIPFactor = 100;

        public int PixelFactor = 100;

        public RectangleF? Bounds => RectangleF.Empty;

        void RegisterServices(FuServiceRepository repository, int hash)
        {
            repository.RegisterService(InputService, hash);
            repository.RegisterService(ViewportService, hash);
        }

        [Fragment]
        public Fu()
        {
            InputService = new InputService(this);
            ViewportService = new ViewportService(this);
        }

        protected readonly CachedProperty<CommonSpace> _space = new(CommonSpace.Normalized);
        public CommonSpace Space => _space.Value;

        public Action<CommonSpace>? OnUpdateSpace { get; set; }

        [Fragment]
        public void SetCommonSpace(CommonSpace space = CommonSpace.Normalized) =>
            _space.TrySetValue(space, (_, next) => OnUpdateSpace?.Invoke(next));

        public bool Notify(INotification notification, CallerInfo caller) =>
            InputService.Notify(notification, caller);

        [Fragment]
        public void Update()
        {
            InputService.Update();
        }

        private bool _invalidate = true;
        private int _callerHash = CallerHashDefault;

        void Invalidate(CallerInfo caller)
        {
            if (_invalidate)
            {
                var appHost = AppHost.Current;
                var repository =
                    appHost.Services.GetService<FuServiceRepository>() ?? new FuServiceRepository();

                _callerHash = caller.GetHashCode();

                RegisterServices(repository, caller.GetHashCode());
            }
            else if (_callerHash != CallerHashDefault && _callerHash != caller.GetHashCode())
            {
                var appHost = AppHost.Current;
                var repository =
                    appHost.Services.GetService<FuServiceRepository>() ?? new FuServiceRepository();
                repository.UnregisterHash(_callerHash);
                _callerHash = caller.GetHashCode();
                RegisterServices(repository, caller.GetHashCode());
            }
        }

        public void Render(CallerInfo caller)
        {
            Invalidate(caller);
            ViewportService.OnRender(caller);
        }

        public void Dispose()
        {
            if (_callerHash != -1)
            {
                var appHost = AppHost.Current;
                var repository =
                    appHost.Services.GetService<FuServiceRepository>() ?? new FuServiceRepository();
                repository.UnregisterHash(_callerHash);
                _callerHash = -1;
            }
        }
    }
}
