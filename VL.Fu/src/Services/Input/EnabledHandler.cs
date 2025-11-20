using System.Reactive.Linq;
using VL.Fu.Core;

namespace VL.Fu.Services.Input
{
    public class EnabledHandler : IObservable<bool>
    {
        protected readonly IObservable<bool> _activeStream;

        public EnabledHandler(Configuration configuration)
        {
            _activeStream = configuration.Enabled.DistinctUntilChanged<bool>().Replay(1).RefCount();
        }

        public IDisposable Subscribe(IObserver<bool> observer)
        {
            return _activeStream.Subscribe(observer);
        }
    }
}
