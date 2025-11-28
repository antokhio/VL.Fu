using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Interaction.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class Draggable : DraggableBase<Vector2>
    {
        [Fragment]
        public Draggable(NodeContext nodeContext)
            : base(nodeContext) { }

        protected override void CalculateNewOffset(FuPointer p)
        {
            if (p.Delta != Vector2.Zero)
            {
                var current = _offsetChannel.Value;
                var next = current + p.Delta;

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

        protected override void CalculateNewOffset(FuPointer p)
        {
            if (p.Delta.X != 0)
            {
                var current = _offsetChannel.Value;
                var next = current + p.Delta.X;

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

        protected override void CalculateNewOffset(FuPointer p)
        {
            if (p.Delta.Y != 0)
            {
                var current = _offsetChannel.Value;
                var next = current + p.Delta.Y;

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

        public override void OnStart(IFuNode host, FuGestureEvent ev)
        {
            base.OnStart(host, ev);

            // 1. Determine a reasonable lever radius based on the host size.
            // This fixes the "super small output" issue in Normalized space.
            float virtualRadius = 100f; // Default for Pixel space / Missing bounds
            if (host.Bounds.HasValue)
            {
                var b = host.Bounds.Value;
                // Average of width/height, divided by 2 for radius
                var sizeAvg = (b.Width + b.Height) / 2.0f;
                if (sizeAvg > 0.0001f)
                {
                    virtualRadius = sizeAvg / 2.0f;
                }
                else
                {
                    // Fallback if bounds are collapsed (e.g. zero size)
                    // If we are in normalized space (inputs are small), use small radius
                    // We can heuristic this by checking the pointer position magnitude?
                    // Or just default to 0.25 for safety if bounds are missing.
                    if (ev.Activator is FuPointer p && Math.Abs(p.Position.X) <= 2.0f)
                        virtualRadius = 0.25f;
                }
            }

            // 2. Initialize the virtual lever based on current Angle value
            var currentCycles = _offsetChannel.Value;
            var rad = currentCycles * MathUtil.TwoPi;

            _virtualLever = new Vector2((float)Math.Cos(rad), (float)Math.Sin(rad)) * virtualRadius;

            // 3. Use VL's Angle logic directly or via helper
            // Using local calculation to ensure independence if library isn't referenced exactly as expected
            _lastPhase = GetAngleInCycles(_virtualLever);
        }

        protected override void CalculateNewOffset(FuPointer p)
        {
            if (p.Delta == Vector2.Zero)
                return;

            // Move internal point
            _virtualLever += p.Delta;

            if (_virtualLever.LengthSquared() < 0.00001f)
                return;

            var currentPhase = GetAngleInCycles(_virtualLever);
            var delta = currentPhase - _lastPhase;

            // Handle wrapping (-0.5 to 0.5 transition)
            if (delta > 0.5f)
                delta -= 1.0f;
            else if (delta < -0.5f)
                delta += 1.0f;

            var next = _offsetChannel.Value + delta;
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

        // Identical to VL.CoreLib Vector2Nodes.Angle logic
        private static float GetAngleInCycles(Vector2 v)
        {
            const double CRadiansToCycles = 1.0 / (2.0 * Math.PI);
            return (float)(Math.Atan2(v.Y, v.X) * CRadiansToCycles);
        }
    }
}
