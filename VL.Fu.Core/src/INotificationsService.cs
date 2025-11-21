using System.Reactive;
using VL.Fu.Core.Input;

namespace VL.Fu.Core
{
    public interface INotificationsService : IContextedService, INotifiable, IDisposable
    {
        IObservable<bool> EnabledStream { get; }
        IObservable<bool> FocusedStream { get; }
        IObservable<Unit> OnFocusFound { get; }
        IObservable<Unit> OnFocusLost { get; }
        IObservable<Unit> OnReset { get; }
        IObservable<FuMouse> MouseStream { get; }
        IObservable<IReadOnlyDictionary<int, FuPointer>> PointersStream { get; }
        IObservable<IReadOnlySet<FuKey>> KeysStream { get; }
        IObservable<bool> TouchActiveStream { get; }
        IObservable<bool> IsActiveStream { get; }
    }
}
