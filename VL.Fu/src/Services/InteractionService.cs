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

        private IReadOnlySet<FuKey> _keysDown = ImmutableHashSet<FuKey>.Empty;

        public void Initialize(NotificationService notificationService)
        {
            _subscriptions.Add(notificationService.PointersStream.Subscribe(OnPointersUpdate));
            _subscriptions.Add(notificationService.MouseStream.Subscribe(OnMouseUpdate));
            _subscriptions.Add(notificationService.KeysDownStream.Subscribe(OnKeysUpdate));
        }

        private void OnPointersUpdate(IReadOnlyDictionary<int, FuPointer> currentPointers)
        {
            // 1. Handle pointers that were released since the last frame
            ProcessPointerReleases(currentPointers);

            // 2. Process all pointers that are currently down or moving
            ProcessActivePointers(currentPointers);

            // 3. Update our cache of pointer states for the next frame
            UpdateLastPointerStates(currentPointers);
        }

        /// <summary>
        /// Handles pointers that were released by calling OnDeactivate on their captured behavior.
        /// </summary>
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
            }
        }

        /// <summary>
        /// Processes currently active (down or move) pointers, dispatching them to captured or potential behaviors.
        /// </summary>
        private void ProcessActivePointers(IReadOnlyDictionary<int, FuPointer> currentPointers)
        {
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
                    // This pointer is already captured, so we just advance the owning behavior.
                    capturedBehavior.OnAdvance(context);
                }
                else
                {
                    // This pointer is not captured. It's either a new pointer or one for which
                    // we are trying to recognize a gesture.
                    ProcessUncapturedPointer(pointer, context);
                }
            }
        }

        /// <summary>
        /// Handles a pointer that has not yet been captured by any behavior.
        /// </summary>
        private void ProcessUncapturedPointer(FuPointer pointer, GestureInputContext context)
        {
            if (_lastPointerStates.ContainsKey(pointer.Id))
            {
                // POINTER MOVE: The pointer was already present in the last frame.
                // We update any gestures that are in a "Possible" state.
                UpdatePossibleGestures(pointer.Id, context);
            }
            else
            {
                // POINTER DOWN: This is a new pointer.
                // We evaluate all gestures on hit-tested behaviors.
                EvaluateGesturesForNewPointer(pointer, context);
            }
        }

        /// <summary>
        /// For a new pointer, iterates through behaviors to find matching or possible gestures.
        /// </summary>
        private void EvaluateGesturesForNewPointer(FuPointer pointer, GestureInputContext context)
        {
            var pointerId = pointer.Id;

            foreach (var (host, behavior) in _sortedBehaviors)
            {
                if (host.HitTest(pointer))
                {
                    foreach (var gesture in behavior.Gestures)
                    {
                        gesture.Reset();
                        gesture.ProcessInput(context);

                        if (gesture.Status == GestureStatus.Matched)
                        {
                            // A gesture matched immediately (e.g., PointerDown).
                            // Activate the behavior, capture the pointer, and stop searching.
                            behavior.OnActivate(gesture);
                            _capturedPointers[pointerId] = behavior;
                            return; // This pointer is now handled.
                        }
                        else if (gesture.Status == GestureStatus.Possible)
                        {
                            // A gesture is now "possible" (e.g., DragGesture after TouchDown).
                            // Add it to the list of active gestures to be evaluated on the next pointer move.
                            if (!_activeGestures.ContainsKey(pointerId))
                                _activeGestures[pointerId] = new();
                            _activeGestures[pointerId].Add((behavior, gesture));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// For a moving pointer, updates all "possible" gestures to see if any have now matched.
        /// </summary>
        private void UpdatePossibleGestures(int pointerId, GestureInputContext context)
        {
            if (!_activeGestures.TryGetValue(pointerId, out var possibleGestures))
                return;

            // Iterate backwards so we can safely remove items.
            for (int i = possibleGestures.Count - 1; i >= 0; i--)
            {
                var (behavior, gesture) = possibleGestures[i];
                gesture.ProcessInput(context);

                if (gesture.Status == GestureStatus.Matched)
                {
                    // A gesture has won! Activate its behavior.
                    behavior.OnActivate(gesture);
                    _capturedPointers[pointerId] = behavior;

                    // Cancel all other gestures that were in the running for this pointer.
                    foreach (var (b, g) in possibleGestures)
                        if (g != gesture)
                            g.Cancel();

                    // Clear the list of possible gestures for this pointer, as it's now captured.
                    _activeGestures.Remove(pointerId);
                    return; // This pointer is now handled.
                }
                else if (gesture.Status == GestureStatus.Failed)
                {
                    // This gesture is no longer viable, remove it from the list.
                    possibleGestures.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Caches the current pointer states to be used in the next frame for comparison.
        /// </summary>
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
