using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Property;
using VL.Fu.Interaction.Gestures;
using VL.Lib.IO;
using VL.Lib.Reactive;

namespace VL.Fu.Interaction.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class Pinchable : BehaviourBase, IFuBehaviour
    {
        [Fragment]
        public float Scale => _scaleChannel.Value;

        [Fragment]
        public bool IsScaling => _isScalingChannel.Value;

        private readonly ChannelProperty<float> _scaleChannel = new(1.0f);
        private readonly ChannelProperty<bool> _isScalingChannel = new(false);

        private float _wheelSensitivity = 0.001f;
        private float _zoomSensitivity = 0.002f;
        private readonly CachedProperty<Vector2> _minMaxScale = new(new Vector2(0.01f, 10.0f));

        [Fragment]
        public Pinchable(NodeContext nodeContext)
            : base(nodeContext)
        {
            Gestures = [new PinchGesture(this), new MouseWheelGesture(this)];
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetScaleChannel(IChannel<float>? channel) => _scaleChannel.SetChannel(channel);

        [Fragment(Order = PinOrder.Action)]
        public void SetIsScalingChannel(IChannel<bool>? channel) =>
            _isScalingChannel.SetChannel(channel);

        [Fragment(Order = PinOrder.Action)]
        public void SetSensitivity(float sensitivity = 0.001f, float zoomSensitivity = 0.002f)
        {
            _wheelSensitivity = sensitivity;
            _zoomSensitivity = zoomSensitivity;
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetMinMaxScale(Vector2 minMax) => _minMaxScale.TrySetValue(minMax);

        public override void OnStart(IFuNode host, FuGestureEvent ev)
        {
            _isScalingChannel.EnsureValue(true);
            ApplyLogic(ev);
        }

        public override void OnUpdate(IFuNode host, FuGestureEvent ev)
        {
            _isScalingChannel.EnsureValue(true);
            ApplyLogic(ev);
        }

        public override void OnFinish(IFuNode host, FuGestureEvent ev)
        {
            _isScalingChannel.EnsureValue(false);
        }

        public override void OnCancel(IFuNode host, FuGestureEvent ev)
        {
            _isScalingChannel.EnsureValue(false);
        }

        private void ApplyLogic(FuGestureEvent ev)
        {
            float currentScale = _scaleChannel.Value;
            float newScale = currentScale;
            bool hasChange = false;

            // 1. Mouse Wheel
            if (ev.Gesture is MouseWheelGesture)
            {
                var delta = ev.InputState.Mouse.WheelDelta.Y;
                if (delta != 0)
                {
                    bool isZoomGesture = ev.InputState.Modifiers.Any(k => k.Key == Keys.ControlKey);
                    float sensitivity = isZoomGesture ? _zoomSensitivity : _wheelSensitivity;

                    newScale = currentScale + (delta * sensitivity * currentScale);
                    hasChange = true;
                }
            }
            // 2. Pinch (Standard Touch)
            // Allow >= 2 pointers
            else if (ev.Gesture is PinchGesture pinch && pinch.Activators.Count >= 2)
            {
                // Use the first two registered pointers as the axis of scaling
                var p1 = pinch.Activators[0];
                var p2 = pinch.Activators[1];

                var currDist = (p1.Position - p2.Position).Length();

                var p1Prev = p1.Position - p1.Delta;
                var p2Prev = p2.Position - p2.Delta;
                var prevDist = (p1Prev - p2Prev).Length();

                if (prevDist > 0.001f)
                {
                    var ratio = currDist / prevDist;
                    newScale = currentScale * ratio;
                    hasChange = true;
                }
            }

            if (hasChange)
            {
                var min = _minMaxScale.Value.X;
                var max = _minMaxScale.Value.Y;
                newScale = MathUtil.Clamp(newScale, min, max);

                if (Math.Abs(newScale - currentScale) > 0.00001f)
                {
                    _scaleChannel.OnNext(newScale);
                }
            }
        }
    }
}
