using System.Reactive;
using System.Reactive.Linq;

namespace VL.Fu.Services.Input
{
    public class ResetHandler : IObservable<Unit>
    {
        protected readonly IObservable<Unit> _signal;

        public ResetHandler(IObservable<bool> enabledHandler, IObservable<bool> focusHandler)
        {
            _signal = Observable
                .CombineLatest(
                    enabledHandler,
                    focusHandler,
                    (enabled, focused) => !enabled || !focused
                )
                .DistinctUntilChanged()
                .Where(needsReset => needsReset)
                .Select(_ => Unit.Default);
        }

        public IDisposable Subscribe(IObserver<Unit> observer)
        {
            return _signal.Subscribe(observer);
        }
    }
}
