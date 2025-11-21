using System.Reactive.Linq;

namespace VL.Fu.Core.Handlers
{
    public class EnabledHandler : IObservable<bool>
    {
        protected readonly IObservable<bool> _activeStream;

        public EnabledHandler(Configuration configuration)
        {
            _activeStream = Observable
                .Defer(() => configuration.Enabled.StartWith(configuration.Enabled.Value))
                .Replay(1)
                .RefCount();
        }

        public IDisposable Subscribe(IObserver<bool> observer)
        {
            return _activeStream.Subscribe(observer);
        }
    }
}
