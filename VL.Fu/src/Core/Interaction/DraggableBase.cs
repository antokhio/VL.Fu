using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Property;
using VL.Fu.Interaction.Gestures;
using VL.Lib.Mathematics;
using VL.Lib.Reactive;

namespace VL.Fu.Interaction.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class DraggableBase<T> : BehaviourBase, IFuBehaviour
    {
        public override bool IsTransient => false;

        [Fragment]
        public T Offset => _offsetChannel.Value;

        [Fragment]
        public bool IsDragging => _isDraggingChannel.Value;

        protected readonly ChannelProperty<T> _offsetChannel = new(default);
        protected readonly ChannelProperty<bool> _isDraggingChannel = new(false);
        protected readonly CachedProperty<Optional<Range<T>>> _bounds = new(
            new Optional<Range<T>>()
        );
        protected readonly DragGesture _gesture;
        private FuPointer? _lastProcessedPointer;

        protected DraggableBase(NodeContext nodeContext)
            : base(nodeContext)
        {
            _gesture = new DragGesture(this);
            Gestures = [_gesture];
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetOffsetChannel(IChannel<T>? offsetChannel) =>
            _offsetChannel.SetChannel(offsetChannel);

        [Fragment(Order = PinOrder.Action)]
        public void SetIsDraggingChannel(IChannel<bool>? isDraggingChannel) =>
            _isDraggingChannel.SetChannel(isDraggingChannel);

        [Fragment(Order = PinOrder.Action)]
        public void SetBounds(Optional<Range<T>> bounds) => _bounds.TrySetValue(bounds);

        public override void OnStart(IFuNode host, FuGestureEvent ev)
        {
            _isDraggingChannel.EnsureValue(true);
            _lastProcessedPointer = null;
        }

        public override void OnUpdate(IFuNode host, FuGestureEvent ev)
        {
            _isDraggingChannel.EnsureValue(true);

            if (ev.Activator is FuPointer p)
            {
                // Check if we already processed pointer, since event aggregation might
                // fire twice on same pointer
                if (_lastProcessedPointer.HasValue && p == _lastProcessedPointer.Value)
                {
                    return;
                }

                _lastProcessedPointer = p;
                CalculateNewOffset(p);
            }
        }

        public override void OnFinish(IFuNode host, FuGestureEvent ev) =>
            _isDraggingChannel.EnsureValue(false);

        public override void OnCancel(IFuNode host, FuGestureEvent ev) =>
            _isDraggingChannel.EnsureValue(false);

        protected abstract void CalculateNewOffset(FuPointer p);
    }
}
