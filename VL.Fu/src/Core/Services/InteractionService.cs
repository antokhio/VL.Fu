using System.Reactive.Disposables;
using System.Reactive.Linq;
using VL.Fu.Core.Context;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Fu.Extensions;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Services
{
    public class InteractionService : IContextedService, IInteractionService
    {
        private readonly IContextProvider _provider;
        private readonly INotificationsService _notifications;
        private readonly CompositeDisposable _subscriptions = new();

        private readonly List<InteractionSession> _activeSessions = new();
        private readonly HashSet<int> _globalCapturedPointers = new();

        public InteractionService(
            IContextProvider provider,
            INotificationsService notificationsService
        )
        {
            _provider = provider;
            _notifications = notificationsService;
            _subscriptions.Add(notificationsService.InputStateStream.Subscribe(OnUpdateInput));
        }

        public void OnUpdateInput(FuInputState inputState)
        {
            if (!inputState.IsEnabled || !inputState.IsFocused)
            {
                CancelAll();
                return;
            }

            // 1. Advance Active Sessions
            for (int i = _activeSessions.Count - 1; i >= 0; i--)
            {
                var session = _activeSessions[i];
                var state = session.Gesture.Advance(session.Host, inputState);

                ProcessGestureResult(session, state, i);
            }

            _globalCapturedPointers.Clear();
            foreach (var session in _activeSessions)
            foreach (var pid in session.CapturedPointerIds)
                _globalCapturedPointers.Add(pid);

            // 2. Discover New Interactions
            if (_provider.Root is IFuNode rootNode)
            {
                ProcessFreePointers(rootNode, inputState);
            }
        }

        private void ProcessGestureResult(
            InteractionSession session,
            FuGestureState state,
            int sessionIndex = -1
        )
        {
            switch (state.Phase)
            {
                case GesturePhase.Possible:
                case GesturePhase.Changed:
                    if (state.Event.HasValue)
                        session.Behaviour.OnUpdate(session.Host, state.Event.Value);
                    break;

                case GesturePhase.Began:
                    // Gesture has committed (e.g. Drag threshold crossed).
                    if (state.Event.HasValue)
                        session.Behaviour.OnUpdate(session.Host, state.Event.Value);

                    // Cancel other sessions sharing the same pointer (e.g. Cancel Click)
                    ResolveConflicts(session);
                    break;

                case GesturePhase.Matched:
                    if (state.Event.HasValue)
                        session.Behaviour.OnStop(session.Host, state.Event.Value, isSuccess: true);

                    CleanupSession(session, sessionIndex);
                    break;

                case GesturePhase.Failed:
                case GesturePhase.Cancelled:
                    if (state.Event.HasValue)
                        session.Behaviour.OnStop(session.Host, state.Event.Value, isSuccess: false);
                    else
                        session.Behaviour.OnCancel(session.Host);

                    CleanupSession(session, sessionIndex);
                    break;
            }
        }

        private void ResolveConflicts(InteractionSession winner)
        {
            if (winner.CapturedPointerIds.Count == 0)
                return;

            for (int i = _activeSessions.Count - 1; i >= 0; i--)
            {
                var other = _activeSessions[i];
                if (other == winner)
                    continue;

                bool conflict = false;
                foreach (var pid in winner.CapturedPointerIds)
                {
                    if (other.IsCapturing(pid))
                    {
                        conflict = true;
                        break;
                    }
                }

                if (conflict)
                {
                    other.Behaviour.OnCancel(other.Host);
                    other.Gesture.Reset();
                    RemoveCapture(other);
                    _activeSessions.RemoveAt(i);
                }
            }
        }

        private void CleanupSession(InteractionSession session, int index)
        {
            session.Gesture.Reset();
            RemoveCapture(session);

            if (index >= 0 && index < _activeSessions.Count)
                _activeSessions.RemoveAt(index);
        }

        private void RemoveCapture(InteractionSession session)
        {
            foreach (var pid in session.CapturedPointerIds)
                _globalCapturedPointers.Remove(pid);
        }

        private void ProcessFreePointers(IFuNode root, FuInputState inputState)
        {
            foreach (var pointer in inputState.Pointers.Values)
            {
                if (pointer.State == TouchNotificationKind.TouchUp)
                    continue;
                if (_globalCapturedPointers.Contains(pointer.Id))
                    continue;

                var hitNode = HitTestDeepest(root, pointer);

                if (hitNode != null)
                {
                    var bubblePath = hitNode.Ancestors().Prepend(hitNode);

                    foreach (var node in bubblePath)
                    {
                        if (node is IFuNode fuNode)
                        {
                            bool nodeCapturedPointer = false;

                            // FIX: Track captured pointers locally for this node scope.
                            // This ensures we don't block subsequent behaviors (like Drag)
                            // just because a previous behavior (like Click) started on the SAME node.
                            var pointersCapturedByThisNode = new List<int>();

                            foreach (var behaviour in fuNode.Behaviours)
                            {
                                if (!behaviour.Enabled)
                                    continue;

                                foreach (var gesture in behaviour.Gestures)
                                {
                                    // AttemptStart checks _globalCapturedPointers.
                                    // Since we haven't committed to global yet, multiple behaviors can start here.
                                    if (
                                        AttemptStart(
                                            fuNode,
                                            behaviour,
                                            gesture,
                                            pointer,
                                            inputState
                                        )
                                    )
                                    {
                                        var newSession = _activeSessions.Last();

                                        if (!behaviour.IsTransient)
                                        {
                                            newSession.CapturePointer(pointer.Id);
                                            pointersCapturedByThisNode.Add(pointer.Id);
                                            nodeCapturedPointer = true;

                                            // Do NOT break here. Allow Click & Drag to coexist initially.
                                        }
                                    }
                                }
                            }

                            // Commit to global to prevent bubbling parents from capturing
                            foreach (var pid in pointersCapturedByThisNode)
                                _globalCapturedPointers.Add(pid);

                            if (nodeCapturedPointer)
                                break;
                        }
                    }
                }
            }
        }

        private bool AttemptStart(
            IFuNode node,
            IFuBehaviour behaviour,
            IFuGesture gesture,
            object activator,
            FuInputState input
        )
        {
            var state = gesture.Match(node, input);

            if (state.IsActive)
            {
                // Ensure the gesture matches the specific pointer we are processing
                if (
                    activator != null
                    && state.Event.HasValue
                    && !object.Equals(state.Event.Value.Activator, activator)
                )
                    return false;

                // Ensure the pointer isn't already captured by a DIFFERENT node (active session)
                if (
                    state.Event.HasValue
                    && state.Event.Value.Activator is FuPointer p
                    && _globalCapturedPointers.Contains(p.Id)
                )
                    return false;

                var session = new InteractionSession(behaviour, gesture, node);
                _activeSessions.Add(session);

                if (state.Event.HasValue)
                    behaviour.OnStart(node, state.Event.Value);

                if (state.Phase == GesturePhase.Began)
                {
                    ResolveConflicts(session);
                }

                if (state.IsTerminal)
                {
                    ProcessGestureResult(session, state, _activeSessions.Count - 1);
                }

                return true;
            }
            return false;
        }

        private void CancelAll()
        {
            foreach (var session in _activeSessions)
            {
                session.Behaviour.OnCancel(session.Host);
                session.Gesture.Reset();
            }
            _activeSessions.Clear();
            _globalCapturedPointers.Clear();
        }

        private IFuNode? HitTestDeepest(IFuNode root, FuPointer pointer)
        {
            if (!root.HitTest(pointer))
                return null;

            foreach (var child in root.Children.Reverse())
            {
                if (child is IFuNode childNode)
                {
                    var result = HitTestDeepest(childNode, pointer);
                    if (result != null)
                        return result;
                }
            }

            return root;
        }

        public void Dispose()
        {
            CancelAll();
            _subscriptions.Dispose();
        }
    }
}
