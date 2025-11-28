using System.Reactive;
using System.Reactive.Linq;
using Stride.Core.Mathematics;
using VL.Fu.Core.Extensions;
using VL.Fu.Core.Input;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Handlers
{
    public class MouseHandler : IObservable<FuMouse>
    {
        protected readonly IObservable<FuMouse> _activeStream;

        public MouseHandler(
            IObservable<INotification> notifications,
            IObservable<FuViewport> viewportStream,
            TouchActiveHandler touchActiveHandler,
            IObservable<Unit> onReset
        )
        {
            var activeNotifications = notifications
                .OfType<MouseNotification>()
                .WithLatestFrom(touchActiveHandler, (n, active) => (n, active))
                .Where(t => !t.active)
                .Select(t => t.n);

            var mouseNotifications = activeNotifications
                .CombineLatest(viewportStream, (notification, viewport) => (notification, viewport))
                .Scan(
                    new FuMouse(),
                    (mouse, state) =>
                    {
                        var viewport = state.viewport;
                        var notification = state.notification;

                        if (notification is NotificationWithPosition notificationWithPosition)
                        {
                            var position = notificationWithPosition.ToCurrentSpace(viewport);

                            var isLeft = mouse.IsLeft;
                            var isRight = mouse.IsRight;
                            var isMiddle = mouse.IsMiddle;
                            var isXButton1 = mouse.IsXButton1;
                            var isXButton2 = mouse.IsXButton2;
                            var buttons = MouseButtons.None;

                            if (notification is MouseButtonNotification bn)
                            {
                                buttons = bn.Buttons;
                                if (notification.Kind == MouseNotificationKind.MouseDown)
                                {
                                    if (bn.Buttons.HasFlag(MouseButtons.Left))
                                        isLeft = true;
                                    if (bn.Buttons.HasFlag(MouseButtons.Right))
                                        isRight = true;
                                    if (bn.Buttons.HasFlag(MouseButtons.Middle))
                                        isMiddle = true;
                                    if (bn.Buttons.HasFlag(MouseButtons.XButton1))
                                        isXButton1 = true;
                                    if (bn.Buttons.HasFlag(MouseButtons.XButton2))
                                        isXButton2 = true;
                                }
                                else if (notification.Kind == MouseNotificationKind.MouseUp)
                                {
                                    if (bn.Buttons.HasFlag(MouseButtons.Left))
                                        isLeft = false;
                                    if (bn.Buttons.HasFlag(MouseButtons.Right))
                                        isRight = false;
                                    if (bn.Buttons.HasFlag(MouseButtons.Middle))
                                        isMiddle = false;
                                    if (bn.Buttons.HasFlag(MouseButtons.XButton1))
                                        isXButton1 = false;
                                    if (bn.Buttons.HasFlag(MouseButtons.XButton2))
                                        isXButton2 = false;
                                }
                            }

                            var wheelDelta = Int2.Zero;
                            if (notification is MouseWheelNotification wn)
                                wheelDelta.Y = wn.WheelDelta;
                            if (notification is MouseHorizontalWheelNotification hwn)
                                wheelDelta.X = hwn.WheelDelta;

                            return new FuMouse
                            {
                                Position = position,
                                Wheel = mouse.Wheel + wheelDelta,
                                WheelDelta = wheelDelta,
                                IsLeft = isLeft,
                                IsRight = isRight,
                                IsMiddle = isMiddle,
                                IsXButton1 = isXButton1,
                                IsXButton2 = isXButton2,
                                Buttons = buttons,
                                State = notification.Kind,
                            };
                        }

                        return mouse;
                    }
                )
                .DistinctUntilChanged();

            var resetEvent = Observable
                .Merge(onReset, touchActiveHandler.OnTouchActive)
                .Select(_ => new FuMouse());

            _activeStream = Observable
                .Merge(mouseNotifications, resetEvent)
                .DistinctUntilChanged()
                .Publish()
                .RefCount();
        }

        public IDisposable Subscribe(IObserver<FuMouse> observer)
        {
            return _activeStream.Subscribe(observer);
        }
    }
}
