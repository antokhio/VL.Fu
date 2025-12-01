using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Interaction.Gestures;

namespace VL.Fu.Interaction.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class Draggable : DraggableBase<Vector2>
    {
        [Fragment]
        public Draggable(NodeContext nodeContext)
            : base(nodeContext) { }

        protected override void CalculateNewOffset(DragGesture gesture)
        {
            // Access delta from the primary activator
            var delta = gesture.Activators[0].Delta;

            if (delta != Vector2.Zero)
            {
                var current = _offsetChannel.Value;
                var next = current + delta;

                if (_bounds.Value.HasValue)
                {
                    var range = _bounds.Value.Value;
                    var minX = Math.Min(range.From.X, range.To.X);
                    var maxX = Math.Max(range.From.X, range.To.X);
                    var minY = Math.Min(range.From.Y, range.To.Y);
                    var maxY = Math.Max(range.From.Y, range.To.Y);

                    next.X = MathUtil.Clamp(next.X, minX, maxX);
                    next.Y = MathUtil.Clamp(next.Y, minY, maxY);
                }

                _offsetChannel.OnNext(next);
            }
        }
    }

    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class DraggableX : DraggableBase<float>
    {
        [Fragment]
        public DraggableX(NodeContext nodeContext)
            : base(nodeContext) { }

        protected override void CalculateNewOffset(DragGesture gesture)
        {
            var deltaX = gesture.Activators[0].Delta.X;

            if (deltaX != 0)
            {
                var current = _offsetChannel.Value;
                var next = current + deltaX;

                if (_bounds.Value.HasValue)
                {
                    var range = _bounds.Value.Value;
                    var min = Math.Min(range.From, range.To);
                    var max = Math.Max(range.From, range.To);
                    next = MathUtil.Clamp(next, min, max);
                }

                _offsetChannel.OnNext(next);
            }
        }
    }

    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class DraggableY : DraggableBase<float>
    {
        [Fragment]
        public DraggableY(NodeContext nodeContext)
            : base(nodeContext) { }

        protected override void CalculateNewOffset(DragGesture gesture)
        {
            var deltaY = gesture.Activators[0].Delta.Y;

            if (deltaY != 0)
            {
                var current = _offsetChannel.Value;
                var next = current + deltaY;

                if (_bounds.Value.HasValue)
                {
                    var range = _bounds.Value.Value;
                    var min = Math.Min(range.From, range.To);
                    var max = Math.Max(range.From, range.To);
                    next = MathUtil.Clamp(next, min, max);
                }

                _offsetChannel.OnNext(next);
            }
        }
    }

    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class DraggablePolar : DraggableBase<float>
    {
        private Vector2 _virtualLever;
        private float _lastPhase;

        [Fragment]
        public DraggablePolar(NodeContext nodeContext)
            : base(nodeContext) { }

        protected override void OnDragStart(DragGesture gesture)
        {
            // Logic moved from OnStart
            var host = gesture.Host;
            if (host == null)
                return;

            // 1. Determine a reasonable lever radius based on the host size.
            float virtualRadius = 100f;
            if (host.Bounds.HasValue)
            {
                var b = host.Bounds.Value;
                var sizeAvg = (b.Width + b.Height) / 2.0f;
                if (sizeAvg > 0.0001f)
                {
                    virtualRadius = sizeAvg / 2.0f;
                }
                else
                {
                    // Fallback using gesture properties instead of raw pointer
                    if (Math.Abs(gesture.StartPosition.X) <= 2.0f)
                        virtualRadius = 0.25f;
                }
            }

            var currentCycles = _offsetChannel.Value;
            var rad = currentCycles * MathUtil.TwoPi;

            _virtualLever = new Vector2((float)Math.Cos(rad), (float)Math.Sin(rad)) * virtualRadius;
            _lastPhase = GetAngleInCycles(_virtualLever);
        }

        protected override void CalculateNewOffset(DragGesture gesture)
        {
            var delta = gesture.Activators[0].Delta;

            if (delta == Vector2.Zero)
                return;

            // Move internal point
            _virtualLever += delta;

            if (_virtualLever.LengthSquared() < 0.00001f)
                return;

            var currentPhase = GetAngleInCycles(_virtualLever);
            var phaseDelta = currentPhase - _lastPhase;

            // Handle wrapping
            if (phaseDelta > 0.5f)
                phaseDelta -= 1.0f;
            else if (phaseDelta < -0.5f)
                phaseDelta += 1.0f;

            var next = _offsetChannel.Value + phaseDelta;
            _lastPhase = currentPhase;

            if (_bounds.Value.HasValue)
            {
                var range = _bounds.Value.Value;
                var min = Math.Min(range.From, range.To);
                var max = Math.Max(range.From, range.To);
                next = MathUtil.Clamp(next, min, max);
            }

            _offsetChannel.OnNext(next);
        }

        private static float GetAngleInCycles(Vector2 v)
        {
            const double CRadiansToCycles = 1.0 / (2.0 * Math.PI);
            return (float)(Math.Atan2(v.Y, v.X) * CRadiansToCycles);
        }
    }
}
