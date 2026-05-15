using System.Reactive;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
using VL.Fu.Interaction.Gestures;
using VL.Lib.Reactive;

namespace VL.Fu.Interaction.Behaviours
{
    /// <summary>
    /// Drag based, X-axis constrained swiper.
    /// Drives <see cref="DraggableBase{T}.Offset"/> from pointer drags and snaps to the
    /// nearest stop when the drag finishes.
    ///
    /// Stops are item positions expressed in the same coordinate space as the container's
    /// content (e.g. item centers in container space). <see cref="Center"/> is the point in
    /// that same space where the snapped item should be aligned (typically the viewport
    /// center). The offset that aligns stop <c>s</c> to <see cref="Center"/> is therefore
    /// <c>Center - s</c>, and that value is what <see cref="CurrentPosition"/> exposes.
    ///
    /// Animation between the current <see cref="DraggableBase{T}.Offset"/> and the snapped
    /// <see cref="CurrentPosition"/> is intentionally not handled here – consumers are
    /// expected to damp <see cref="DraggableBase{T}.Offset"/> towards
    /// <see cref="CurrentPosition"/> in their own update loop, optionally using the
    /// signed <see cref="Distance"/> output.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class SwiperX : DraggableBase<float>, IDisposable
    {
        private readonly ChannelProperty<int> _currentIndexChannel = new(0);
        private readonly ChannelProperty<float> _currentPositionChannel = new(0f);
        private readonly ChannelProperty<float> _distanceChannel = new(0f);

        private readonly CachedProperty<IReadOnlyList<float>> _stops = new(Array.Empty<float>());
        private readonly CachedProperty<float> _center = new(0f);

        private IChannel<Unit>? _nextChannel;
        private IChannel<Unit>? _previousChannel;
        private IChannel<int>? _goToChannel;

        private IDisposable? _nextSubscription;
        private IDisposable? _previousSubscription;
        private IDisposable? _goToSubscription;

        private readonly IDisposable _offsetSubscription;
        private readonly IDisposable _indexSubscription;
        private readonly IDisposable _isDraggingSubscription;

        [Fragment]
        public SwiperX(NodeContext nodeContext)
            : base(nodeContext)
        {
            // Recompute distance whenever offset changes (drag, external write, damping).
            _offsetSubscription = _offsetChannel.Subscribe(_ => UpdateDistance());

            // When the index changes (from snap, GoTo, Next, Previous, or external),
            // recompute the target position and distance.
            _indexSubscription = _currentIndexChannel.Subscribe(_ => UpdateCurrentPosition());

            // Snap to the closest stop on drag end / cancel.
            _isDraggingSubscription = _isDraggingChannel.Subscribe(isDragging =>
            {
                if (!isDragging)
                    SnapToClosest();
            });
        }

        [Fragment]
        public int CurrentIndex => _currentIndexChannel.Value;

        [Fragment]
        public float CurrentPosition => _currentPositionChannel.Value;

        /// <summary>
        /// Signed distance from the current <see cref="DraggableBase{T}.Offset"/> to the
        /// snapped <see cref="CurrentPosition"/>. Positive values mean the target lies
        /// in the positive X direction. Use this to drive external damping.
        /// </summary>
        [Fragment]
        public float Distance => _distanceChannel.Value;

        [Fragment(Order = PinOrder.Action)]
        public void SetStops(IReadOnlyList<float>? stops)
        {
            // Stops are expected to be immutable; reference equality is sufficient here.
            var newStops = stops ?? Array.Empty<float>();
            if (ReferenceEquals(_stops.Value, newStops))
                return;

            _stops.SetValue(newStops);

            var clamped = ClampIndex(_currentIndexChannel.Value);
            if (clamped != _currentIndexChannel.Value)
                _currentIndexChannel.OnNext(clamped);
            else
                UpdateCurrentPosition();
        }

        /// <summary>
        /// World-space coordinate (in the same space as <see cref="SetStops"/>) where the
        /// snapped item should be aligned. Typically the viewport / container center along X.
        /// Defaults to 0.
        /// </summary>
        [Fragment(Order = PinOrder.Action)]
        public void SetCenter(float center = 0f)
        {
            if (_center.SetValue(center))
                UpdateCurrentPosition();
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetCurrentIndexChannel(IChannel<int>? channel) =>
            _currentIndexChannel.SetChannel(channel);

        [Fragment(Order = PinOrder.Action)]
        public void SetCurrentPositionChannel(IChannel<float>? channel) =>
            _currentPositionChannel.SetChannel(channel);

        [Fragment(Order = PinOrder.Action)]
        public void SetDistanceChannel(IChannel<float>? channel) =>
            _distanceChannel.SetChannel(channel);

        [Fragment(Order = PinOrder.Action)]
        public void SetNextChannel(IChannel<Unit>? channel)
        {
            if (ReferenceEquals(_nextChannel, channel))
                return;

            _nextSubscription?.Dispose();
            _nextSubscription = null;
            _nextChannel = channel;

            if (channel != null)
                _nextSubscription = channel.Subscribe(_ => Next());
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetPreviousChannel(IChannel<Unit>? channel)
        {
            if (ReferenceEquals(_previousChannel, channel))
                return;

            _previousSubscription?.Dispose();
            _previousSubscription = null;
            _previousChannel = channel;

            if (channel != null)
                _previousSubscription = channel.Subscribe(_ => Previous());
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetGoToChannel(IChannel<int>? channel)
        {
            if (ReferenceEquals(_goToChannel, channel))
                return;

            _goToSubscription?.Dispose();
            _goToSubscription = null;
            _goToChannel = channel;

            if (channel != null)
                _goToSubscription = channel.Subscribe(GoTo);
        }

        public void Next() => GoTo(_currentIndexChannel.Value + 1);

        public void Previous() => GoTo(_currentIndexChannel.Value - 1);

        public void GoTo(int index)
        {
            var clamped = ClampIndex(index);
            // Always push so external listeners receive the request even if the index
            // didn't change (e.g. re-issuing GoTo after manual offset shift).
            if (clamped == _currentIndexChannel.Value)
                UpdateCurrentPosition();
            else
                _currentIndexChannel.OnNext(clamped);
        }

        protected override void CalculateNewOffset(DragGesture gesture)
        {
            var deltaX = gesture.Activators[0].Delta.X;
            if (deltaX == 0f)
                return;

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

        private int ClampIndex(int index)
        {
            var stops = _stops.Value;
            if (stops.Count == 0)
                return 0;
            return Math.Clamp(index, 0, stops.Count - 1);
        }

        private float TargetOffsetFor(int index)
        {
            // Offset that aligns stops[index] (item position in container space)
            // to Center (alignment point in container space).
            return _center.Value - _stops.Value[index];
        }

        private void SnapToClosest()
        {
            var stops = _stops.Value;
            if (stops.Count == 0)
                return;

            var offset = _offsetChannel.Value;
            int bestIndex = 0;
            float bestDistance = Math.Abs(TargetOffsetFor(0) - offset);

            for (int i = 1; i < stops.Count; i++)
            {
                var d = Math.Abs(TargetOffsetFor(i) - offset);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestIndex = i;
                }
            }

            if (bestIndex != _currentIndexChannel.Value)
                _currentIndexChannel.OnNext(bestIndex);
            else
                UpdateCurrentPosition();
        }

        private void UpdateCurrentPosition()
        {
            var stops = _stops.Value;
            float position;

            if (stops.Count == 0)
            {
                // No stops configured – the current offset is effectively the target.
                position = _offsetChannel.Value;
            }
            else
            {
                var index = ClampIndex(_currentIndexChannel.Value);
                position = TargetOffsetFor(index);
            }

            if (!EqualityComparer<float>.Default.Equals(_currentPositionChannel.Value, position))
                _currentPositionChannel.OnNext(position);

            UpdateDistance();
        }

        private void UpdateDistance()
        {
            var distance = _currentPositionChannel.Value - _offsetChannel.Value;
            if (!EqualityComparer<float>.Default.Equals(_distanceChannel.Value, distance))
                _distanceChannel.OnNext(distance);
        }

        public override void Dispose()
        {
            _offsetSubscription?.Dispose();
            _indexSubscription?.Dispose();
            _isDraggingSubscription?.Dispose();

            base.Dispose();
        }
    }
}
