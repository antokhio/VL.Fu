using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Core.InstanceId;
using VL.Fu.Core.Repository;

namespace VL.Fu.Services
{
    /// <summary>
    /// Manages and processes all interactive behaviors in a scene.
    /// It maintains a priority-sorted list of behaviors for efficient hit-testing.
    /// </summary>
    public class InteractionService : IRepositoryService
    {
        private readonly CompositeDisposable _subscriptions = new();

        // Stores all registered behaviors, keyed by the behavior's instance ID for fast updates.
        private readonly ConcurrentDictionary<
            int,
            (IInteractiveHost Host, IFuBehaviour Behaviour)
        > _behaviors = new();

        // A sorted list used for fast input processing. Rebuilt whenever the main dictionary changes.
        private volatile IReadOnlyList<(
            IInteractiveHost Host,
            IFuBehaviour Behaviour
        )> _sortedBehaviors = ImmutableList<(IInteractiveHost Host, IFuBehaviour Behaviour)>.Empty;

        // State management for interactions (placeholders for now)
        private readonly ConcurrentDictionary<int, IFuBehaviour> _capturedPointers = new();
        private IReadOnlySet<FuKey> _keysDown = ImmutableHashSet<FuKey>.Empty;

        /// <summary>
        /// Initializes the service and subscribes to input streams.
        /// </summary>
        public void Initialize(NotificationService notificationService)
        {
            _subscriptions.Add(notificationService.PointersStream.Subscribe(OnPointersUpdate));
            _subscriptions.Add(notificationService.MouseStream.Subscribe(OnMouseUpdate));
            _subscriptions.Add(notificationService.KeysDownStream.Subscribe(OnKeysUpdate));
        }

        private void OnPointersUpdate(IReadOnlyDictionary<int, FuPointer> pointers)
        {
            // Main interaction logic will go here.
            // 1. Iterate through active pointers.
            // 2. Check if a pointer is captured.
            // 3. If not, traverse _sortedBehaviors and perform hit-tests.
            // 4. Dispatch events (OnPointerDown, OnPointerMove, OnPointerUp) to the target behavior.
        }

        private void OnMouseUpdate(FuMouse mouse)
        {
            // Mouse-specific logic. Could be unified with pointers.
            // 1. Check for hover events by hit-testing against _sortedBehaviors.
            // 2. Dispatch mouse-specific events if needed (e.g., OnMouseWheel).
        }

        private void OnKeysUpdate(IReadOnlySet<FuKey> keys)
        {
            _keysDown = keys;
            // This can be used to modify pointer interactions (e.g., holding Shift to multi-select).
        }

        public void UpdateBehaviors(IInteractiveHost host, IEnumerable<IFuBehaviour> behaviors)
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
            _sortedBehaviors = ImmutableList<(IInteractiveHost Host, IFuBehaviour Behaviour)>.Empty;
        }
    }
}
