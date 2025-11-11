using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using VL.Fu.Core;
using VL.Fu.Core.Behaviour;
using VL.Fu.Core.Gesture;
using VL.Fu.Core.Input;
using VL.Fu.Core.InstanceId;
using VL.Fu.Core.Repository;

namespace VL.Fu.Services
{
    public class InteractionService : IRepositoryService
    {
        private readonly CompositeDisposable _subscriptions = new();

        private readonly ConcurrentDictionary<
            int,
            (IInteractiveHost Host, IInteractiveBehavior Behaviour)
        > _behaviors = new();
        private volatile IReadOnlyList<(
            IInteractiveHost Host,
            IInteractiveBehavior Behaviour
        )> _sortedBehaviors = ImmutableList<(IInteractiveHost, IInteractiveBehavior)>.Empty;

        // --- State management for interactions ---
        private readonly ConcurrentDictionary<int, IInteractiveBehavior> _capturedPointers = new();
        private readonly Dictionary<int, FuPointer> _lastPointerStates = new();
        private readonly Dictionary<
            int,
            List<(IInteractiveBehavior Behaviour, IGesture Gesture)>
        > _activeGestures = new();
        private readonly ConcurrentDictionary<int, IInteractiveBehavior> _activeTransientBehaviors =
            new();
        private IReadOnlySet<FuKey> _keysDown = ImmutableHashSet<FuKey>.Empty;

        public void Initialize(NotificationService notificationService)
        {
            _subscriptions.Add(notificationService.PointersStream.Subscribe(OnPointersUpdate));
            _subscriptions.Add(notificationService.MouseStream.Subscribe(OnMouseUpdate));
            _subscriptions.Add(notificationService.KeysDownStream.Subscribe(OnKeysUpdate));
        }

        private void OnPointersUpdate(IReadOnlyDictionary<int, FuPointer> currentPointers)
        {
            ProcessPointerReleases(currentPointers);
            ProcessActivePointers(currentPointers);
            UpdateLastPointerStates(currentPointers);
        }

        private void ProcessPointerReleases(IReadOnlyDictionary<int, FuPointer> currentPointers)
        {
            var releasedPointerIds = _lastPointerStates.Keys.Except(currentPointers.Keys).ToList();
            foreach (var pointerId in releasedPointerIds)
            {
                if (_capturedPointers.TryRemove(pointerId, out var capturedBehavior))
                    capturedBehavior.OnDeactivate();

                if (_activeGestures.Remove(pointerId, out var gesturesToCancel))
                    foreach (var (_, gesture) in gesturesToCancel)
                        gesture.Cancel();

                if (_activeTransientBehaviors.TryRemove(pointerId, out var transientBehavior))
                    transientBehavior.OnCancel();
            }
        }

        private void ProcessActivePointers(IReadOnlyDictionary<int, FuPointer> currentPointers)
        {
            ProcessTransientBehaviorCancellations(currentPointers);

            foreach (var pointer in currentPointers.Values)
            {
                var context = new GestureInputContext(
                    pointer,
                    currentPointers,
                    _keysDown,
                    pointer.TimeStamp
                );

                if (_capturedPointers.TryGetValue(pointer.Id, out var capturedBehavior))
                {
                    // This is the part that was not being fired. It should now work correctly.
                    capturedBehavior.OnAdvance(context);
                }
                else
                {
                    // This pointer is not captured. It could be new, or moving while gestures are being evaluated.
                    ProcessUncapturedPointer(pointer, context);
                }
            }
        }

        private void ProcessTransientBehaviorCancellations(
            IReadOnlyDictionary<int, FuPointer> currentPointers
        )
        {
            foreach (var (pointerId, transientBehavior) in _activeTransientBehaviors)
            {
                if (
                    currentPointers.TryGetValue(pointerId, out var pointer)
                    && transientBehavior is IInstanceId instance
                )
                {
                    if (_behaviors.TryGetValue(instance.InstanceId, out var hostBehaviorPair))
                    {
                        if (!hostBehaviorPair.Host.HitTest(pointer))
                        {
                            if (
                                _activeTransientBehaviors.TryRemove(
                                    pointerId,
                                    out var behaviorToCancel
                                )
                            )
                            {
                                behaviorToCancel.OnCancel();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Correctly handles an uncaptured pointer by distinguishing between a new "down" event
        /// and a "move" event for a pointer that is already being tracked for possible gestures.
        /// </summary>
        private void ProcessUncapturedPointer(FuPointer pointer, GestureInputContext context)
        {
            // First, regardless of down/move, check for transient behaviors (like hover).
            EvaluateTransientGestures(pointer, context);

            if (_lastPointerStates.ContainsKey(pointer.Id))
            {
                // POINTER MOVE: The pointer existed last frame. Update "possible" gestures (like drag).
                UpdatePossibleGestures(pointer.Id, context);
            }
            else
            {
                // POINTER DOWN: This is a new pointer. Evaluate all gestures.
                EvaluateCapturingGesturesForNewPointer(pointer, context);
            }
        }

        /// <summary>
        /// Iterates through all behaviors to find and activate matching transient gestures.
        /// This runs for every uncaptured pointer movement.
        /// </summary>
        private void EvaluateTransientGestures(FuPointer pointer, GestureInputContext context)
        {
            foreach (var (host, behavior) in _sortedBehaviors)
            {
                if (behavior.IsTransient && host.HitTest(pointer))
                {
                    foreach (var gesture in behavior.Gestures)
                    {
                        gesture.Reset();
                        gesture.ProcessInput(context);
                        if (gesture.Status == GestureStatus.Matched)
                        {
                            // If it's already active, don't re-activate.
                            if (!_activeTransientBehaviors.ContainsKey(pointer.Id))
                            {
                                behavior.OnActivate(gesture);
                                _activeTransientBehaviors[pointer.Id] = behavior;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// For a new pointer, evaluates non-transient gestures to find an immediate match or a possible gesture.
        /// </summary>
        private void EvaluateCapturingGesturesForNewPointer(
            FuPointer pointer,
            GestureInputContext context
        )
        {
            var pointerId = pointer.Id;
            foreach (var (host, behavior) in _sortedBehaviors)
            {
                // Skip transient behaviors in this pass
                if (behavior.IsTransient)
                    continue;

                if (host.HitTest(pointer))
                {
                    foreach (var gesture in behavior.Gestures)
                    {
                        gesture.Reset();
                        gesture.ProcessInput(context);

                        if (gesture.Status == GestureStatus.Matched)
                        {
                            // Immediate match (e.g., PointerDown), capture and we are done.
                            behavior.OnActivate(gesture);
                            _capturedPointers[pointerId] = behavior;
                            return; // Pointer is captured, exit.
                        }
                        else if (gesture.Status == GestureStatus.Possible)
                        {
                            // A gesture is now "possible" (e.g., DragGesture after TouchDown).
                            if (!_activeGestures.ContainsKey(pointerId))
                                _activeGestures[pointerId] = new();
                            _activeGestures[pointerId].Add((behavior, gesture));
                        }
                    }
                }
            }
        }

        private void UpdatePossibleGestures(int pointerId, GestureInputContext context)
        {
            if (!_activeGestures.TryGetValue(pointerId, out var possibleGestures))
                return;

            for (int i = possibleGestures.Count - 1; i >= 0; i--)
            {
                var (behavior, gesture) = possibleGestures[i];
                gesture.ProcessInput(context);

                if (gesture.Status == GestureStatus.Matched)
                {
                    // A "possible" gesture has now matched. Capture the pointer.
                    behavior.OnActivate(gesture);
                    _capturedPointers[pointerId] = behavior;

                    foreach (var (b, g) in possibleGestures)
                        if (g != gesture)
                            g.Cancel();

                    _activeGestures.Remove(pointerId);
                    return; // Pointer is now captured.
                }
                else if (gesture.Status == GestureStatus.Failed)
                {
                    possibleGestures.RemoveAt(i);
                }
            }
        }

        private void UpdateLastPointerStates(IReadOnlyDictionary<int, FuPointer> currentPointers)
        {
            _lastPointerStates.Clear();
            foreach (var (id, pointer) in currentPointers)
            {
                _lastPointerStates[id] = pointer;
            }
        }

        private void OnMouseUpdate(
            FuMouse mouse
        ) { /* TODO */
        }

        private void OnKeysUpdate(IReadOnlySet<FuKey> keys) => _keysDown = keys;

        public void UpdateBehaviors(
            IInteractiveHost host,
            IEnumerable<IInteractiveBehavior> behaviors
        )
        {
            var hostInstanceId = host.InstanceId;
            var behaviorsToRemove = _behaviors
                .Where(kvp => kvp.Value.Host.InstanceId == hostInstanceId)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in behaviorsToRemove)
            {
                _behaviors.TryRemove(key, out _);
            }

            foreach (var behavior in behaviors)
            {
                if (behavior is IInstanceId behaviorWithId)
                    _behaviors[behaviorWithId.InstanceId] = (host, behavior);
            }
            _sortedBehaviors = _behaviors
                .Values.OrderByDescending(b => b.Behaviour.Priority)
                .ToImmutableList();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _behaviors.Clear();
            _sortedBehaviors = ImmutableList<(IInteractiveHost, IInteractiveBehavior)>.Empty;
        }
    }
}
