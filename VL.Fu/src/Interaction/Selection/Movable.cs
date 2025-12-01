using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Property;
using VL.Fu.Core.Selection;
using VL.Fu.Interaction.Gestures;
using VL.Lib.IO;
using VL.Lib.Reactive;

namespace VL.Fu.Interaction.Selection
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class Movable : BehaviourBase, IFuBehaviour
    {
        public override bool IsTransient => false;

        [Fragment]
        public Vector2 Position => _positionChannel.Value;

        [Fragment]
        public bool IsDragging => _isDraggingChannel.Value;

        private readonly ChannelProperty<Vector2> _positionChannel = new(Vector2.Zero);
        private readonly ChannelProperty<bool> _isDraggingChannel = new(false);

        private readonly DragGesture _dragGesture;

        // Gestures
        private readonly KeyDownGesture _leftGesture;
        private readonly KeyDownGesture _rightGesture;
        private readonly KeyDownGesture _upGesture;
        private readonly KeyDownGesture _downGesture;

        // Config
        private float _stepSize = 0.01f;
        private float _fineStep = 0.001f;
        private float _largeStep = 0.1f;

        private FuPointer? _lastProcessedPointer;

        [Fragment]
        public Movable(NodeContext nodeContext)
            : base(nodeContext)
        {
            _dragGesture = new DragGesture(this);

            // Define reusable handler for keys
            void HandleKey(KeyDownGesture g, Vector2 dir, bool isTick)
            {
                if (g.Host == null)
                    return;

                // Determine step size
                float step = _stepSize;
                if (g.CurrentModifiers.Any(k => k.Key == Keys.ShiftKey))
                    step = _largeStep;
                else if (g.CurrentModifiers.Any(k => k.Key == Keys.ControlKey))
                    step = _fineStep;

                var delta = dir * step;

                // On Start: Ensure Selection
                if (!isTick)
                {
                    var provider = g.Host.FindSelectionProvider();
                    if (provider != null && !provider.IsSelected(g.Host))
                        provider.Select(g.Host, false, false);
                }

                ApplyMovement(delta, g.Host);
            }

            _leftGesture = new KeyDownGesture(
                this,
                Keys.Left,
                onStart: g => HandleKey(g, -Vector2.UnitX, false),
                onTick: g => HandleKey(g, -Vector2.UnitX, true)
            );

            _rightGesture = new KeyDownGesture(
                this,
                Keys.Right,
                onStart: g => HandleKey(g, Vector2.UnitX, false),
                onTick: g => HandleKey(g, Vector2.UnitX, true)
            );

            _upGesture = new KeyDownGesture(
                this,
                Keys.Up,
                onStart: g => HandleKey(g, -Vector2.UnitY, false),
                onTick: g => HandleKey(g, -Vector2.UnitY, true)
            );

            _downGesture = new KeyDownGesture(
                this,
                Keys.Down,
                onStart: g => HandleKey(g, Vector2.UnitY, false),
                onTick: g => HandleKey(g, Vector2.UnitY, true)
            );

            Gestures = [_dragGesture, _leftGesture, _rightGesture, _upGesture, _downGesture];
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetPositionChannel(IChannel<Vector2>? c) => _positionChannel.SetChannel(c);

        [Fragment(Order = PinOrder.Action)]
        public void SetSteps(float step = 0.01f, float fine = 0.001f, float large = 0.1f)
        {
            _stepSize = step;
            _fineStep = fine;
            _largeStep = large;
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetThreshold(float threshold) => _dragGesture.Threshold = threshold;

        public void SetSelected(bool selected) { }

        // --- Update Loop (Only Drag) ---

        public override void OnStart(IFuNode host, FuGestureEvent ev)
        {
            if (ev.Gesture == _dragGesture)
                HandleDrag(host, ev);
        }

        public override void OnUpdate(IFuNode host, FuGestureEvent ev)
        {
            if (ev.Gesture == _dragGesture)
                HandleDrag(host, ev);
        }

        public override void OnFinish(IFuNode host, FuGestureEvent ev)
        {
            if (ev.Gesture == _dragGesture)
                _isDraggingChannel.OnNext(false);
        }

        public override void OnCancel(IFuNode host, FuGestureEvent ev)
        {
            if (ev.Gesture == _dragGesture)
                _isDraggingChannel.OnNext(false);
        }

        private void HandleDrag(IFuNode host, FuGestureEvent ev)
        {
            _isDraggingChannel.OnNext(true);

            if (_dragGesture.Status == GestureStatus.Start)
            {
                var provider = host.FindSelectionProvider();
                if (provider != null && !provider.IsSelected(host))
                    provider.Select(host, false, false);
            }

            if (ev.Activator is FuPointer p)
            {
                if (_lastProcessedPointer.HasValue && p == _lastProcessedPointer.Value)
                    return;
                _lastProcessedPointer = p;

                if (p.Delta != Vector2.Zero)
                    ApplyMovement(p.Delta, host);
            }
        }

        // --- Shared ---

        private void ApplyMovement(Vector2 delta, IFuNode? host)
        {
            ApplyDelta(delta, host);

            if (host != null)
            {
                var provider = host.FindSelectionProvider();
                if (provider != null && provider.IsSelected(host))
                {
                    provider.DispatchToSelected(
                        n =>
                        {
                            var m = n.Behaviours.OfType<Movable>().FirstOrDefault();
                            m?.ApplyDelta(delta, n);
                        },
                        exclude: host
                    );
                }
            }
        }

        public void ApplyDelta(Vector2 delta, IFuNode? host)
        {
            var next = _positionChannel.Value + delta;
            _positionChannel.OnNext(next);
        }

        public override void Dispose()
        {
            _leftGesture.Dispose();
            _rightGesture.Dispose();
            _upGesture.Dispose();
            _downGesture.Dispose();
            base.Dispose();
        }
    }
}
