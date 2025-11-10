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
            // --- 1. Process UP events ---
            var releasedPointerIds = _lastPointerStates.Keys.Except(currentPointers.Keys).ToList();
            foreach (var pointerId in releasedPointerIds)
            {
                if (_capturedPointers.TryRemove(pointerId, out var capturedBehavior))
                    capturedBehavior.OnDeactivate();

                if (_activeGestures.Remove(pointerId, out var gesturesToCancel))
                    foreach (var (_, gesture) in gesturesToCancel)
                        gesture.Cancel();
            }

            // --- 2. Process DOWN and MOVE events ---
            foreach (var pointer in currentPointers.Values)
            {
                var context = new GestureInputContext(
                    pointer,
                    currentPointers,
                    _keysDown,
                    pointer.TimeStamp
                );
                var pointerId = pointer.Id;

                if (_lastPointerStates.ContainsKey(pointerId))
                {
                    // --- POINTER MOVE ---
                    if (_capturedPointers.TryGetValue(pointerId, out var capturedBehavior))
                    {
                        // A behavior has already captured this pointer. Just advance it.
                        capturedBehavior.OnAdvance(context);
                    }
                    else if (_activeGestures.TryGetValue(pointerId, out var possibleGestures))
                    {
                        // No capture yet, but there are gestures waiting for more input.
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

                                // Clear the list of possible gestures for this pointer.
                                _activeGestures.Remove(pointerId);

                                // We have a winner for this pointer, move to the next one.
                                goto nextPointer;
                            }
                            else if (gesture.Status == GestureStatus.Failed)
                            {
                                // This gesture is no longer viable, remove it from the list.
                                possibleGestures.RemoveAt(i);
                            }
                        }
                    }
                }
                else
                {
                    // --- POINTER DOWN ---
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
                                    behavior.OnActivate(gesture);
                                    _capturedPointers[pointerId] = behavior;
                                    goto nextPointer;
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
                nextPointer:
                ;
            }

            // --- 3. Update pointer state for the next frame ---
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
