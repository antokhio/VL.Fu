using System.Reactive.Linq;
using VL.Fu.Core.Common;

namespace VL.Fu.Core.Handlers
{
    public class ActivityHandler : IObservable<bool>
    {
        protected readonly IObservable<bool> _activeStream;

        public ActivityHandler(
            MouseHandler mouse,
            KeysHandler keys,
            PointersHandler pointers,
            TimeSpan? timeout = null
        )
        {
            var inputActivityTimeout = timeout ?? Constants.DefaultInputActivityTimeout;

            var acitivityEvents = Observable.Merge(
                mouse.Select(_ => true),
                keys.Select(_ => true),
                pointers.Select(_ => true)
            );

            _activeStream = acitivityEvents
                .Select(_ =>
                    Observable
                        .Return(true)
                        .Concat(Observable.Return(false).Delay(inputActivityTimeout))
                )
                .Switch()
                .StartWith(false)
                .DistinctUntilChanged()
                .Replay(1)
                .RefCount();
        }

        public IDisposable Subscribe(IObserver<bool> observer)
        {
            return _activeStream.Subscribe(observer);
        }
    }
}
