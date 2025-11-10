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

        // State management for interactions
        private readonly ConcurrentDictionary<int, IInteractiveBehavior> _capturedPointers = new();
        private readonly Dictionary<int, FuPointer> _pointerStates = new();
        private readonly Dictionary<int, List<(IInteractiveBehavior, IGesture)>> _activeGestures =
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
            // --- 1. Process existing pointers (Move, Up) ---
            var previousPointerIds = _pointerStates.Keys.ToList();
            foreach (var pointerId in previousPointerIds)
            {
                // Pointer was released (UP)
                if (!currentPointers.ContainsKey(pointerId))
                {
                    // For now, we just clean up state.
                    // Later, this is where OnDeactivate and TapGesture logic would go.
                    _pointerStates.Remove(pointerId);
                    _activeGestures.Remove(pointerId);
                    if (_capturedPointers.TryRemove(pointerId, out var capturedBehavior))
                    {
                        capturedBehavior.OnDeactivate();
                    }
                }
                // Pointer has moved (MOVE) - We will implement this in the next step
                else
                {
                    // This is where OnAdvance logic for captured pointers would go.
                }
            }

            // --- 2. Process new pointers (DOWN) ---
            foreach (var pointer in currentPointers.Values)
            {
                if (
                    !_pointerStates.ContainsKey(pointer.Id)
                    && pointer.State == TouchNotificationKind.TouchDown
                )
                {
                    // This is a new pointer, start the activation sequence.
                    var context = new GestureInputContext(
                        pointer,
                        currentPointers,
                        _keysDown,
                        pointer.TimeStamp
                    );

                    // Find the winning behavior
                    foreach (var (host, behavior) in _sortedBehaviors)
                    {
                        // A. Hit Test
                        if (host.HitTest(pointer))
                        {
                            // B. Gesture Matching
                            var gestures = behavior.GetGestures();
                            foreach (var gesture in gestures)
                            {
                                gesture.Reset();
                                gesture.ProcessInput(context);

                                if (gesture.Status == GestureStatus.Matched)
                                {
                                    // C. Activation
                                    behavior.OnActivate(gesture);

                                    // Simple capture logic: the first behavior to match a gesture captures the pointer.
                                    _capturedPointers[pointer.Id] = behavior;

                                    // We found our winner, stop processing for this pointer.
                                    goto nextPointer;
                                }
                                else if (gesture.Status == GestureStatus.Possible)
                                {
                                    // This gesture is interested, keep it for later (e.g., for a drag).
                                    if (!_activeGestures.ContainsKey(pointer.Id))
                                        _activeGestures[pointer.Id] =
                                            new List<(IInteractiveBehavior, IGesture)>();
                                    _activeGestures[pointer.Id].Add((behavior, gesture));
                                }
                            }
                        }
                    }
                }
                nextPointer:
                ;
            }

            // --- 3. Update pointer state for the next frame ---
            _pointerStates.Clear();
            foreach (var kvp in currentPointers)
            {
                _pointerStates[kvp.Key] = kvp.Value;
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
                {
                    _behaviors[behaviorWithId.InstanceId] = (host, behavior);
                }
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
