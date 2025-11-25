using System.Reactive.Disposables;
using System.Reactive.Linq;
using VL.Fu.Core.Context;
using VL.Fu.Core.Input;
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
            if (!inputState.IsEnabled)
            {
                CancelAll();
                return;
            }

            // 1. Cleanup Finished Sessions
            for (int i = _activeSessions.Count - 1; i >= 0; i--)
            {
                var session = _activeSessions[i];
                if (session.IsFinished)
                {
                    _activeSessions.RemoveAt(i);
                    foreach (var pid in session.CapturedPointerIds)
                        _globalCapturedPointers.Remove(pid);

                    session.Gesture.Reset();
                }
            }

            // 2. Advance Active Sessions
            for (int i = _activeSessions.Count - 1; i >= 0; i--)
            {
                var session = _activeSessions[i];

                if (session.Gesture.Advance(session.Host, inputState, out var evt))
                {
                    if (evt.HasValue)
                        session.Behaviour.OnAdvance(session.Host, evt.Value);
                }
                else
                {
                    if (evt.HasValue)
                        session.Behaviour.OnDeactivate(session.Host, evt.Value);
                    else
                        session.Behaviour.OnCancel(session.Host);
                }
            }

            _globalCapturedPointers.Clear();
            foreach (var session in _activeSessions)
            foreach (var pid in session.CapturedPointerIds)
                _globalCapturedPointers.Add(pid);

            // 3. Discover New Interactions
            if (_provider.Root is IFuNode rootNode)
            {
                ProcessFreePointers(rootNode, inputState);
                ProcessKeys(rootNode, inputState);
            }
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
                            if (
                                TryStartInteraction(fuNode, pointer, inputState, out var newSession)
                            )
                            {
                                _activeSessions.Add(newSession);

                                if (!newSession.Behaviour.IsTransient)
                                {
                                    newSession.CapturePointer(pointer.Id);
                                    _globalCapturedPointers.Add(pointer.Id);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ProcessKeys(IFuNode root, FuInputState inputState)
        {
            if (inputState.Keys.Count == 0)
                return;

            if (TryStartInteraction(root, null, inputState, out var newSession))
            {
                _activeSessions.Add(newSession);
            }
        }

        private bool TryStartInteraction(
            IFuNode node,
            object? activator,
            FuInputState input,
            out InteractionSession session
        )
        {
            session = null;

            foreach (var behaviour in node.Behaviours)
            {
                if (!behaviour.Enabled)
                    continue;

                foreach (var gesture in behaviour.Gestures)
                {
                    if (gesture.Match(node, input, out var evt))
                    {
                        // FIX: Use object.Equals for value type structural equality
                        if (
                            activator != null
                            && evt.HasValue
                            && !object.Equals(evt.Value.Activator, activator)
                        )
                            continue;

                        if (
                            evt.HasValue
                            && evt.Value.Activator is FuPointer p
                            && _globalCapturedPointers.Contains(p.Id)
                        )
                            continue;

                        session = new InteractionSession(behaviour, gesture, node);

                        if (evt.HasValue)
                            behaviour.OnActivate(node, evt.Value);

                        return true;
                    }
                }
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
