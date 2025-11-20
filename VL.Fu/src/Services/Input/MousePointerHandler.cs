using System.Reactive.Linq;
using VL.Fu.Core.Input;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services.Input
{
    public class MousePointerHandler : IObservable<FuPointer>
    {
        protected readonly IObservable<FuPointer> _activeStream;

        public MousePointerHandler(IObservable<FuMouse> mouseStream)
        {
            _activeStream = mouseStream
                .Scan(
                    FuPointer.DefaultMousePointer,
                    (previousPointer, mouseState) =>
                    {
                        if (previousPointer.IsLeft is false && mouseState.IsLeft)
                        {
                            return previousPointer.WithMouseState(
                                mouseState,
                                TouchNotificationKind.TouchDown
                            );
                        }
                        else if (previousPointer.IsLeft && mouseState.IsLeft is false)
                        {
                            return previousPointer.WithMouseState(
                                mouseState,
                                TouchNotificationKind.TouchUp
                            );
                        }
                        else
                        {
                            return previousPointer.WithMouseState(
                                mouseState,
                                TouchNotificationKind.TouchMove
                            );
                        }
                    }
                )
                .Skip(1)
                .Publish()
                .RefCount();
        }

        public IDisposable Subscribe(IObserver<FuPointer> observer)
        {
            return _activeStream.Subscribe(observer);
        }
    }
}
