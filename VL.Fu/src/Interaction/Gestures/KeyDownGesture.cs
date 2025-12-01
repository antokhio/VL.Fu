using System.Collections.Immutable;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using VL.Core.Import;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO;

namespace VL.Fu.Interaction.Gestures
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class KeyDownGesture : KeyGesture, IDisposable
    {
        // Callbacks
        private readonly Action<KeyDownGesture>? _onStart;
        private readonly Action<KeyDownGesture>? _onFinish;
        private readonly Action<KeyDownGesture>? _onTick;

        // Configuration
        public TimeSpan RepeatDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan RepeatInterval { get; set; } = TimeSpan.FromMilliseconds(50);

        // State
        private IDisposable? _timer;

        /// <summary>
        /// Cached modifiers from the last Evaluation. Useful for checking Shift/Ctrl state inside OnTick.
        /// </summary>
        public IReadOnlySet<FuKey> CurrentModifiers { get; private set; } =
            ImmutableHashSet<FuKey>.Empty;

        public KeyDownGesture(
            IFuBehaviour behaviour,
            Keys key,
            Keys modifiers = Keys.None,
            Action<KeyDownGesture>? onStart = null,
            Action<KeyDownGesture>? onFinish = null,
            Action<KeyDownGesture>? onTick = null
        )
            : base(behaviour, key, modifiers)
        {
            _onStart = onStart;
            _onFinish = onFinish;
            _onTick = onTick;
        }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            // Update modifiers for the callback context
            CurrentModifiers = inputState.Modifiers;

            bool match = CheckConditions(inputState);

            if (
                Status == GestureStatus.Idle
                || Status == GestureStatus.Finish
                || Status == GestureStatus.Cancel
            )
            {
                if (match)
                {
                    Status = GestureStatus.Start;
                    _onStart?.Invoke(this);
                    StartTimer();
                }
                else
                {
                    Status = GestureStatus.Idle;
                }
            }
            else // Active
            {
                if (match)
                {
                    Status = GestureStatus.Update;
                }
                else
                {
                    Status = GestureStatus.Finish;
                    StopTimer();
                    _onFinish?.Invoke(this);
                }
            }
        }

        private void StartTimer()
        {
            StopTimer();

            // Timer runs on Default scheduler (ThreadPool)
            _timer = Observable
                .Interval(RepeatInterval, Scheduler.Default)
                .DelaySubscription(RepeatDelay)
                .Subscribe(_ =>
                {
                    // Fire tick callback
                    _onTick?.Invoke(this);
                });
        }

        private void StopTimer()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public override void Reset()
        {
            base.Reset();
            StopTimer();
        }

        public void Dispose()
        {
            StopTimer();
        }
    }
}
