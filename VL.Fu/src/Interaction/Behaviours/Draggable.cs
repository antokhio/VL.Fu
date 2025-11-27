using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Property;
using VL.Fu.Interaction.Gestures;
using VL.Lib.Reactive;

namespace VL.Fu.Interaction.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class Draggable : BehaviourBase, IFuBehaviour
    {
        public override bool IsTransient => false;

        // Use Drag Priority (800) unless overridden
        public override int Priority =>
            _priority.Value == GesturePriority.None ? GesturePriority.Drag : _priority.Value;

        [Fragment]
        public Vector2 Offset => _offsetChannel.Value;

        [Fragment]
        public bool IsDragging => _isDraggingChannel.Value;

        private readonly ChannelProperty<Vector2> _offsetChannel = new(Vector2.Zero);
        private readonly ChannelProperty<bool> _isDraggingChannel = new(false);
        private readonly DragGesture _gesture;

        [Fragment]
        public Draggable(NodeContext nodeContext)
            : base(nodeContext)
        {
            _gesture = new DragGesture(this);
            Gestures = [_gesture];
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetOffsetChannel(IChannel<Vector2>? channel) =>
            _offsetChannel.SetChannel(channel);

        [Fragment(Order = PinOrder.Action)]
        public void SetIsDraggingChannel(IChannel<bool>? channel) =>
            _isDraggingChannel.SetChannel(channel);

        // -- Lifecycle Hooks --

        public override void OnStart(IFuNode host, FuGestureEvent ev)
        {
            _isDraggingChannel.EnsureValue(true);
        }

        public override void OnUpdate(IFuNode host, FuGestureEvent ev)
        {
            _isDraggingChannel.EnsureValue(true);

            // Apply Delta
            if (ev.Gesture is DragGesture drag && drag.Delta != Vector2.Zero)
            {
                var current = _offsetChannel.Value;
                _offsetChannel.OnNext(current + drag.Delta);
            }
        }

        public override void OnFinish(IFuNode host, FuGestureEvent ev)
        {
            _isDraggingChannel.EnsureValue(false);
        }

        public override void OnCancel(IFuNode host, FuGestureEvent ev)
        {
            _isDraggingChannel.EnsureValue(false);
        }
    }
}
