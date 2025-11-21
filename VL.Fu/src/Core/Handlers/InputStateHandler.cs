using System.Collections.Immutable;
using System.Reactive.Linq;
using VL.Fu.Core.Input;

namespace VL.Fu.Core.Handlers
{
    public class InputStateHandler : IObservable<FuInputState>
    {
        private readonly IObservable<FuInputState> _inputStateStream;

        public InputStateHandler(INotificationsService notificationsService)
        {
            _inputStateStream = Observable
                .CombineLatest(
                    notificationsService.PointersStream.StartWith(
                        ImmutableDictionary<int, FuPointer>.Empty
                    ),
                    notificationsService.KeysStream.StartWith(ImmutableHashSet<FuKey>.Empty),
                    notificationsService.MouseStream.StartWith(new FuMouse()),
                    notificationsService.EnabledStream.StartWith(true),
                    notificationsService.FocusedStream.StartWith(true),
                    (pointers, allKeys, mouse, enabled, focused) =>
                    {
                        var keys = allKeys.Where(k => !k.IsModifier()).ToHashSet();
                        var modifiers = allKeys.Where(k => k.IsModifier()).ToHashSet();

                        return new FuInputState(pointers, keys, modifiers, mouse, enabled, focused);
                    }
                )
                .Replay(1)
                .RefCount();
        }

        public IDisposable Subscribe(IObserver<FuInputState> observer)
        {
            return _inputStateStream.Subscribe(observer);
        }
    }
}
