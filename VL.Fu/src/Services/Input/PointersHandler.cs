using System.Reactive;
using VL.Fu.Core.Input;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services.Input
{
    public class PointersHandler : IObservable<IReadOnlyDictionary<int, FuPointer>>
    {
        protected readonly IObservable<IReadOnlyDictionary<int, FuPointer>> _activeStream;

        public PointersHandler(
            IObservable<INotification> notifications,
            IObservable<FuMouse> mouse,
            IObservable<bool> touchActive,
            IObservable<Unit> onReset
        )
        {
            // TODO:
        }

        public IDisposable Subscribe(IObserver<IReadOnlyDictionary<int, FuPointer>> observer)
        {
            throw new NotImplementedException();
        }
    }
}
