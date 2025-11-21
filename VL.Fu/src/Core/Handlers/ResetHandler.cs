using System.Reactive;
using System.Reactive.Linq;

namespace VL.Fu.Core.Handlers
{
    public class ResetHandler : IObservable<Unit>
    {
        protected readonly IObservable<Unit> _activeStream;

        public ResetHandler(IObservable<bool> focusStream, IObservable<bool> enabledStream)
        {
            _activeStream = Observable
                .Merge(focusStream, enabledStream)
                .Where(x => x is false)
                .Select(_ => Unit.Default);
        }

        public IDisposable Subscribe(IObserver<Unit> observer)
        {
            return _activeStream.Subscribe(observer);
        }
    }
}
