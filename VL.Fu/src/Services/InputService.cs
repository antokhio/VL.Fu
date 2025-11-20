using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Services.Input;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Services
{
    public class InputService : InstancedId, IInputService
    {
        public IObservable<bool> FocusedStream { get; }
        public IObservable<Unit> ResetSignal { get; }

        private readonly Subject<FuInputState> _inputStateStream = new();
        public IObservable<FuInputState> InputStateStream => _inputStateStream.AsObservable();
        public IObservable<bool> IsTouchActiveStream { get; }
        public IObservable<IReadOnlyDictionary<int, FuPointer>> PointersStream { get; }
        public IObservable<FuMouse> MouseStream { get; }
        public IObservable<FuPointer> MousePointerStream { get; }
        public IObservable<IReadOnlySet<FuKey>> KeysStream { get; }

        private readonly Subject<INotification> _notifications = new();
        private readonly CompositeDisposable _subscriptions = new();

        public InputService(Configuration configuration, IViewportService viewportService)
        {
            // OnFocusLost
            // OnFocusFound
            // IsEnabled
            // Reset

            FocusedStream = new FocusHandler(_notifications);

            var enabledNotifications = new EnabledHandler(_notifications, configuration);

            ResetSignal = new ResetHandler(FocusedStream, configuration.Enabled);

            IsTouchActiveStream = new TouchActiveHandler(enabledNotifications);

            MouseStream = new MouseHandler(
                enabledNotifications,
                viewportService.ViewportStream,
                ResetSignal
            );

            MousePointerStream = new MousePointerHandler(MouseStream);
            KeysStream = new KeysHandler(enabledNotifications, ResetSignal);

            //KeysStream = new KeyboardService(
            //    enabledNotifications,
            //    configuration.Enabled
            //).KeysStream;

            //// --- Pointer Handling ---
            //var pointerEvents = Observable.Merge(_touchService.Pointers, _mouseService.Pointers);

            //var pointerResetEvents = enabledNotifications
            //    .OfType<LostFocusNotification>()
            //    .Select(_ => FuPointerEvent.Reset())
            //    .Merge(
            //        configuration
            //            .Enabled.Where(enabled => !enabled)
            //            .Select(_ => FuPointerEvent.Reset())
            //    );

            //PointersStream = pointerEvents
            //    .Merge(pointerResetEvents)
            //    .Scan(
            //        ImmutableDictionary<int, FuPointer>.Empty,
            //        (pointers, evt) =>
            //            evt.Type switch
            //            {
            //                FuPointerEventType.Cleanup => pointers.Remove(evt.PointerId),
            //                FuPointerEventType.Reset => ImmutableDictionary<int, FuPointer>.Empty,
            //                FuPointerEventType.Update =>
            //                // For any update, the incoming pointer is the most current source of truth.
            //                // We simply add or replace it in the dictionary.
            //                // This handles TouchDown, TouchMove, and TouchUp correctly.
            //                pointers.SetItem(evt.Pointer.Id, evt.Pointer),
            //                _ => pointers,
            //            }
            //    )
            //    .StartWith(ImmutableDictionary<int, FuPointer>.Empty)
            //    .Publish()
            //    .RefCount();

            //// --- Combine all public streams into a single FuInputState ---
            //var combinedInputStream = Observable.CombineLatest(
            //    PointersStream,
            //    MouseStream,
            //    KeysStream,
            //    (pointers, mouse, keysDown) =>
            //    {
            //        var keys = ImmutableHashSet.CreateBuilder<FuKey>();
            //        var modifiers = ImmutableHashSet.CreateBuilder<FuKey>();
            //        foreach (var key in keysDown)
            //        {
            //            if (key.IsModifier())
            //                modifiers.Add(key);
            //            else
            //                keys.Add(key);
            //        }

            //        return new FuInputState(
            //            pointers,
            //            keys.ToImmutable(),
            //            modifiers.ToImmutable(),
            //            mouse
            //        );
            //    }
            //);

            //_subscriptions.Add(combinedInputStream.Subscribe(_inputStateStream));
        }

        public void Notify(INotification notification) => _notifications.OnNext(notification);

        public void Dispose()
        {
            _notifications.Dispose();
            _subscriptions.Dispose();
            _inputStateStream.Dispose();
        }
    }
}
