using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Fu.Core.Extensions;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Selection;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Services
{
    public class InteractionService : IContextedService, IInteractionService
    {
        private readonly IContextProvider _provider;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly BehaviorSubject<IReadOnlyList<IFuGesture>> _gestureDispatch = new(
            ImmutableList<IFuGesture>.Empty
        );
        public IObservable<IReadOnlyList<IFuGesture>> GestureDispatch => _gestureDispatch;

        private readonly HashSet<IFuGesture> _activeGestures = new();

        public InteractionService(
            IContextProvider provider,
            INotificationsService notificationsService
        )
        {
            _provider = provider;
            _subscriptions.Add(notificationsService.InputStateStream.Subscribe(OnUpdateInput));
        }

        public void OnUpdateInput(FuInputState inputState)
        {
            var dispatchList = new List<IFuGesture>();

            if (
                !_provider.TryGetViewportStream(out _)
                || _provider.Root is not IFuNode root
                || !inputState.IsEnabled
            )
            {
                CancelAll(inputState);
                _gestureDispatch.OnNext(ImmutableList<IFuGesture>.Empty);
                return;
            }

            // --- 1. Discovery & Pre-Calculation ---
            var candidatesList = new List<IFuGesture>();
            var seenCandidates = new HashSet<IFuGesture>();
            var gesturePreCalculatedCandidates = new Dictionary<IFuGesture, List<FuPointer>>();
            var pointerHitCount = new Dictionary<int, int>();

            // Track which providers we have already processed to avoid duplicate recursion
            var seenProviders = new HashSet<ISelectionProvider>();

            // Helper to register a node's gestures for evaluation
            void RegisterNodeGestures(IFuNode node, FuPointer? hitPointer)
            {
                if (node == null)
                    return;

                foreach (var behaviour in node.Behaviours)
                {
                    if (!behaviour.Enabled)
                        continue;

                    // Check if this behavior is a Selection Provider
                    // If we hit a provider, we include its selected nodes in the candidate list.
                    // This ensures KeyGestures on selected nodes are found even if not directly hovered.
                    if (behaviour is ISelectionProvider provider && seenProviders.Add(provider))
                    {
                        foreach (var selectedNode in provider.SelectedNodes)
                        {
                            // Recursively register selected nodes.
                            // We pass 'null' for pointer because these are found via state, not hit test.
                            RegisterNodeGestures(selectedNode, null);
                        }
                    }

                    foreach (var gesture in behaviour.Gestures)
                    {
                        gesture.Host = node;

                        if (seenCandidates.Add(gesture))
                            candidatesList.Add(gesture);

                        if (!gesturePreCalculatedCandidates.TryGetValue(gesture, out var list))
                        {
                            list = new List<FuPointer>();
                            gesturePreCalculatedCandidates[gesture] = list;
                        }

                        // Only add the pointer if this registration came from a direct Hit Test
                        if (hitPointer.HasValue)
                            list.Add(hitPointer.Value);
                    }
                }
            }

            if (inputState.IsFocused)
            {
                // A. Hit Test Discovery
                foreach (var pointer in inputState.Pointers.Values)
                {
                    if (pointer.State == TouchNotificationKind.TouchUp)
                        continue;

                    foreach (var node in HitTestNodes(root, pointer))
                    {
                        if (!pointerHitCount.ContainsKey(pointer.Id))
                            pointerHitCount[pointer.Id] = 0;
                        pointerHitCount[pointer.Id]++;

                        RegisterNodeGestures(node, pointer);
                    }
                }

                // B. Root Provider Discovery
                // Ensure we check the Root for a SelectionProvider even if it wasn't explicitly hit
                // (though usually root covers bounds, this handles edge cases or non-hit-testable roots).
                var rootProvider = root.Behaviours.OfType<ISelectionProvider>().FirstOrDefault();
                if (rootProvider != null && seenProviders.Add(rootProvider))
                {
                    foreach (var selectedNode in rootProvider.SelectedNodes)
                    {
                        RegisterNodeGestures(selectedNode, null);
                    }
                }
            }

            // Include currently active gestures (e.g. ongoing Drags)
            foreach (var active in _activeGestures)
            {
                if (seenCandidates.Add(active))
                    candidatesList.Add(active);

                if (!gesturePreCalculatedCandidates.ContainsKey(active))
                    gesturePreCalculatedCandidates[active] = new List<FuPointer>();
            }

            // --- 2. Evaluation Phase ---
            foreach (var gesture in candidatesList)
            {
                if (gesturePreCalculatedCandidates.TryGetValue(gesture, out var ptrs))
                {
                    if (ptrs.Count > 1)
                    {
                        ptrs.Sort(
                            (a, b) =>
                            {
                                int countA = pointerHitCount.GetValueOrDefault(a.Id, int.MaxValue);
                                int countB = pointerHitCount.GetValueOrDefault(b.Id, int.MaxValue);
                                return countA.CompareTo(countB);
                            }
                        );
                    }
                    gesture.Evaluate(inputState, ptrs);
                }
            }

            // --- 3. Conflict Resolution Phase ---
            var pairs = candidatesList.SelectMany(g =>
                g.Activators.Select(p => new { Gesture = g, PointerId = p.Id })
            );

            var groups = pairs.GroupBy(x => x.PointerId);

            foreach (var group in groups)
            {
                // Winner Logic:
                // 1. Must be Active (Start/Update)
                // 2. Must NOT be Transient (Transient gestures don't block others)
                // 3. Highest Priority wins
                var winners = group
                    .Select(x => x.Gesture)
                    .Where(g =>
                        (g.Status == GestureStatus.Start || g.Status == GestureStatus.Update)
                        && !g.Behaviour.IsTransient
                    )
                    .OrderByDescending(g => g.Priority)
                    .Distinct()
                    .ToList();

                if (winners.Any())
                {
                    var primaryWinner = winners.First();

                    foreach (var item in group)
                    {
                        var gesture = item.Gesture;
                        if (gesture == primaryWinner)
                            continue;

                        // IMPORTANT: Transient gestures are never cancelled by conflict resolution.
                        if (gesture.Behaviour.IsTransient)
                            continue;

                        if (
                            gesture.Status == GestureStatus.Finish
                            || gesture.Status == GestureStatus.Cancel
                            || gesture.Status == GestureStatus.Idle
                        )
                            continue;

                        bool isLowerPriority = gesture.Priority < primaryWinner.Priority;
                        bool isSamePriorityAndActive =
                            (gesture.Priority == primaryWinner.Priority)
                            && (
                                gesture.Status == GestureStatus.Start
                                || gesture.Status == GestureStatus.Update
                            );

                        if (isLowerPriority || isSamePriorityAndActive)
                        {
                            DispatchToBehaviour(gesture, GestureStatus.Cancel, inputState);
                            gesture.Reset();
                        }
                    }
                }
            }

            // --- 4. Dispatch Phase ---
            _activeGestures.Clear();

            foreach (var gesture in candidatesList)
            {
                if (gesture.Status == GestureStatus.Idle || gesture.Host == null)
                    continue;

                DispatchToBehaviour(gesture, gesture.Status, inputState);

                if (
                    gesture.Status == GestureStatus.Finish
                    || gesture.Status == GestureStatus.Cancel
                )
                {
                    gesture.Reset();
                }
                else
                {
                    _activeGestures.Add(gesture);
                    dispatchList.Add(gesture);
                }
            }

            dispatchList.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            _gestureDispatch.OnNext(dispatchList.ToImmutableList());
        }

        private void DispatchToBehaviour(
            IFuGesture gesture,
            GestureStatus status,
            FuInputState input
        )
        {
            if (gesture.Host == null)
                return;
            var primary = gesture.Activators.FirstOrDefault();
            var evt = new FuGestureEvent(gesture, primary, input);

            switch (status)
            {
                case GestureStatus.Start:
                    gesture.Behaviour.OnStart(gesture.Host, evt);
                    break;
                case GestureStatus.Update:
                    gesture.Behaviour.OnUpdate(gesture.Host, evt);
                    break;
                case GestureStatus.Finish:
                    gesture.Behaviour.OnFinish(gesture.Host, evt);
                    break;
                case GestureStatus.Cancel:
                    gesture.Behaviour.OnCancel(gesture.Host, evt);
                    break;
            }
        }

        private void CancelAll(FuInputState inputState)
        {
            foreach (var gesture in _activeGestures)
            {
                DispatchToBehaviour(gesture, GestureStatus.Cancel, inputState);
                gesture.Reset();
            }
            _activeGestures.Clear();
        }

        private IEnumerable<IFuNode> HitTestNodes(IFuNode root, FuPointer pointer)
        {
            if (!root.HitTest(pointer))
                yield break;
            foreach (var child in root.Children.Reverse())
            {
                if (child is IFuNode childNode)
                {
                    foreach (var node in HitTestNodes(childNode, pointer))
                        yield return node;
                }
            }
            yield return root;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _gestureDispatch.OnCompleted();
        }
    }
}
