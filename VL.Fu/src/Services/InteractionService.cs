using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using VL.Fu.Core;
using VL.Fu.Core.Behaviour;
using VL.Fu.Core.Gesture;
using VL.Fu.Core.Input;
using VL.Fu.Core.InstanceId;
using VL.Fu.Core.Repository;
using VL.Lib.IO.Notifications;

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
                if (_activeGestures.TryGetValue(pointerId, out var possibleGesturesForUp))
                {
                    var lastState = _lastPointerStates[pointerId];
                    var upContext = new GestureInputContext(
                        lastState.WithState(TouchNotificationKind.TouchUp),
                        currentPointers,
                        _keysDown,
                        lastState.TimeStamp
                    );

                    foreach (var (behavior, gesture) in possibleGesturesForUp)
                    {
                        gesture.ProcessInput(upContext);
                        if (gesture.Status == GestureStatus.Matched)
                        {
                            behavior.OnActivate(gesture);
                            _capturedPointers.TryRemove(pointerId, out _);
                            goto pointerHandled;
                        }
                    }
                }

                if (_capturedPointers.TryRemove(pointerId, out var capturedBehavior))
                {
                    // We need the final state of the pointer to pass to OnDeactivate.
                    var lastState = _lastPointerStates[pointerId];
                    var upContext = new GestureInputContext(
                        lastState.WithState(TouchNotificationKind.TouchUp),
                        currentPointers,
                        _keysDown,
                        lastState.TimeStamp
                    );
                    capturedBehavior.OnDeactivate(upContext);
                }

                pointerHandled:
                if (_activeGestures.Remove(pointerId, out var gesturesToCancel))
                {
                    foreach (var (_, gesture) in gesturesToCancel)
                        gesture.Cancel();
                }
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
                        capturedBehavior.OnAdvance(context);
                    }
                    else if (_activeGestures.TryGetValue(pointerId, out var possibleGestures))
                    {
                        for (int i = possibleGestures.Count - 1; i >= 0; i--)
                        {
                            var (behavior, gesture) = possibleGestures[i];

                            // Allow the behavior to react to gesture state changes during the move.
                            // This is key for Hoverable to detect when its gesture fails.
                            behavior.OnAdvance(context);

                            gesture.ProcessInput(context);

                            if (gesture.Status == GestureStatus.Matched)
                            {
                                behavior.OnActivate(gesture);

                                if (!behavior.IsTransient)
                                {
                                    _capturedPointers[pointerId] = behavior;

                                    // Cancel other pending gestures
                                    foreach (var (b, g) in possibleGestures)
                                        if (g != gesture)
                                            g.Cancel();
                                    _activeGestures.Remove(pointerId);

                                    goto nextPointer; // Winner found
                                }
                                else
                                {
                                    // If transient, like Hoverable, reset the gesture immediately
                                    // so it can check for new states on the next frame.
                                    gesture.Reset();
                                }

                                // It was transient, so we remove it from the list of possibles, but keep searching.
                                possibleGestures.RemoveAt(i);
                            }
                            else if (gesture.Status == GestureStatus.Failed)
                            {
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
                                    // Always activate on match.
                                    behavior.OnActivate(gesture);

                                    // Only capture and stop if the behavior is NOT transient.
                                    if (!behavior.IsTransient)
                                    {
                                        _capturedPointers[pointerId] = behavior;
                                        goto nextPointer;
                                    }
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
