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

            // Handle captured pointers, including cancelling them if they become disabled.
            foreach (var (pointerId, capturedBehavior) in _capturedPointers)
            {
                if (!capturedBehavior.Enabled)
                {
                    if (_capturedPointers.TryRemove(pointerId, out var behaviorToCancel))
                    {
                        behaviorToCancel.OnCancel();
                    }
                    continue; // Skip to next captured pointer
                }

                if (currentPointers.TryGetValue(pointerId, out var pointer))
                {
                    var capturedContext = new GestureInputContext(
                        pointer,
                        currentPointers,
                        _keysDown,
                        pointer.TimeStamp
                    );
                    capturedBehavior.OnAdvance(capturedContext);
                }
            }

            // Process uncaptured pointers for new interactions
            foreach (var pointer in currentPointers.Values)
            {
                if (!_capturedPointers.ContainsKey(pointer.Id))
                {
                    ProcessUncapturedPointer(pointer, currentPointers);
                }
            }
        }

        private void ProcessTransientBehaviorCancellations(
            IReadOnlyDictionary<int, FuPointer> currentPointers
        )
        {
            foreach (var (pointerId, transientBehavior) in _activeTransientBehaviors)
            {
                var shouldCancel = !transientBehavior.Enabled;

                if (
                    !shouldCancel
                    && currentPointers.TryGetValue(pointerId, out var pointer)
                    && transientBehavior is IInstanceId instance
                )
                {
                    if (_behaviors.TryGetValue(instance.InstanceId, out var hostBehaviorPair))
                    {
                        if (!hostBehaviorPair.Host.HitTest(pointer))
                        {
                            shouldCancel = true;
                        }
                    }
                }

                if (shouldCancel)
                {
                    if (_activeTransientBehaviors.TryRemove(pointerId, out var behaviorToCancel))
                    {
                        behaviorToCancel.OnCancel();
                    }
                }
            }
        }

        private void ProcessUncapturedPointer(
            FuPointer pointer,
            IReadOnlyDictionary<int, FuPointer> allPointers
        )
        {
            EvaluateTransientGestures(pointer, allPointers);

            if (_lastPointerStates.ContainsKey(pointer.Id))
            {
                UpdatePossibleGestures(pointer, allPointers);
            }
            else
            {
                EvaluateCapturingGesturesForNewPointer(pointer, allPointers);
            }
        }

        private void EvaluateTransientGestures(
            FuPointer pointer,
            IReadOnlyDictionary<int, FuPointer> allPointers
        )
        {
            var context = new GestureInputContext(
                pointer,
                allPointers,
                _keysDown,
                pointer.TimeStamp
            );
            foreach (var (host, behavior) in _sortedBehaviors)
            {
                if (behavior.Enabled && behavior.IsTransient && host.HitTest(pointer))
                {
                    foreach (var gesture in behavior.Gestures)
                    {
                        gesture.Reset();
                        gesture.ProcessInput(context);
                        if (gesture.Status == GestureStatus.Matched)
                        {
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

        private void EvaluateCapturingGesturesForNewPointer(
            FuPointer pointer,
            IReadOnlyDictionary<int, FuPointer> allPointers
        )
        {
            var pointerId = pointer.Id;
            var context = new GestureInputContext(
                pointer,
                allPointers,
                _keysDown,
                pointer.TimeStamp
            );
            foreach (var (host, behavior) in _sortedBehaviors)
            {
                if (!behavior.Enabled || behavior.IsTransient)
                    continue;

                if (host.HitTest(pointer))
                {
                    foreach (var gesture in behavior.Gestures)
                    {
                        gesture.Reset();
                        gesture.ProcessInput(context);

                        if (gesture.Status == GestureStatus.Matched)
                        {
                            behavior.OnActivate(gesture);
                            _capturedPointers[pointerId] = behavior;
                            return;
                        }
                        else if (gesture.Status == GestureStatus.Possible)
                        {
                            if (!_activeGestures.ContainsKey(pointerId))
                                _activeGestures[pointerId] = new();
                            _activeGestures[pointerId].Add((behavior, gesture));
                        }
                    }
                }
            }
        }

        private void UpdatePossibleGestures(
            FuPointer pointer,
            IReadOnlyDictionary<int, FuPointer> allPointers
        )
        {
            var pointerId = pointer.Id;
            if (!_activeGestures.TryGetValue(pointerId, out var possibleGestures))
                return;

            var context = new GestureInputContext(
                pointer,
                allPointers,
                _keysDown,
                pointer.TimeStamp
            );
            for (int i = possibleGestures.Count - 1; i >= 0; i--)
            {
                var (behavior, gesture) = possibleGestures[i];

                if (!behavior.Enabled)
                    continue;

                gesture.ProcessInput(context);

                if (gesture.Status == GestureStatus.Matched)
                {
                    behavior.OnActivate(gesture);
                    if (!behavior.IsTransient)
                    {
                        _capturedPointers[pointerId] = behavior;
                    }

                    foreach (var (b, g) in possibleGestures)
                        if (g != gesture)
                            g.Cancel();

                    _activeGestures.Remove(pointerId);
                    return;
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
