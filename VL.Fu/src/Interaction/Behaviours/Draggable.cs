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
        // Draggable captures the pointer, so it is NOT transient.
        public override bool IsTransient => false;

        [Fragment]
        public Vector2 Offset => _offsetChannel.Value;

        [Fragment]
        public bool IsDragging => _isDraggingChannel.Value;

        // Renamed to Offset to reflect that it holds the accumulated position
        private readonly ChannelProperty<Vector2> _offsetChannel = new(Vector2.Zero);
        private readonly ChannelProperty<bool> _isDraggingChannel = new(false);
        private readonly DragGesture _gesture;

        [Fragment]
        public Draggable(NodeContext nodeContext)
            : base(nodeContext)
        {
            _gesture = new DragGesture(nodeContext);
            Gestures = new[] { _gesture };
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetOffsetChannel(IChannel<Vector2>? channel) =>
            _offsetChannel.SetChannel(channel);

        [Fragment(Order = PinOrder.Action)]
        public void SetIsDraggingChannel(IChannel<bool>? channel) =>
            _isDraggingChannel.SetChannel(channel);

        public override void OnStart(IFuNode host, FuGestureEvent ev)
        {
            _isDraggingChannel.EnsureValue(false);
        }

        public override void OnUpdate(IFuNode host, FuGestureEvent ev)
        {
            if (ev.Gesture is DragGesture drag)
            {
                if (drag.HasStartedDragging)
                {
                    _isDraggingChannel.EnsureValue(true);

                    if (drag.Delta != Vector2.Zero)
                    {
                        // Accumulate the Frame Delta into the existing Offset/Position
                        var current = _offsetChannel.Value;
                        _offsetChannel.OnNext(current + drag.Delta);
                    }
                }
            }
        }

        public override void OnStop(IFuNode host, FuGestureEvent ev, bool isSuccess)
        {
            _isDraggingChannel.EnsureValue(false);
        }

        public override void OnCancel(IFuNode host)
        {
            _isDraggingChannel.EnsureValue(false);
        }
    }
}
