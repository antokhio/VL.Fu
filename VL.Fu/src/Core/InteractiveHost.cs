using VL.Core.Import;
using VL.Fu.Core.Behaviour;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
using VL.Fu.Services;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core
{
    /// <summary>
    /// An abstract base class that combines rendering, hit-testing, and the ability to host interactive behaviors.
    /// It acts as a bridge to the InteractionService, registering its behaviors for centralized processing.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class InteractiveHost : HitTestableBase, IBehavior, IInteractiveHost
    {
        private readonly CachedProperty<Spread<IInteractiveBehavior>> _behaviours = new(
            Spread<IInteractiveBehavior>.Empty
        );

        private InteractionService? _interactionService;

        private bool _behaviorsNeedRegistration = false;

        /// <summary>
        /// Sets the spread of composable behaviors to be hosted by this node.
        /// </summary>
        [Fragment(Order = PinOrder.Behaviour)]
        public void SetBehaviours(Spread<IInteractiveBehavior> behaviours) =>
            _behaviours.TrySetValue(
                behaviours,
                (prev, next) =>
                {
                    _behaviorsNeedRegistration = true;
                    RegisterBehaviorsWithService();
                }
            );

        public override void SetContextId(int contextId)
        {
            var contextChanged = contextId != this.ContextId;
            base.SetContextId(contextId);

            if (contextChanged)
            {
                // The context has changed, which means we MUST re-register our behaviors
                // with the service belonging to the new context.
                _behaviorsNeedRegistration = true;

                // Propagate the new context ID to all current behaviors.
                foreach (var behaviour in _behaviours.Value)
                {
                    behaviour?.SetContextId(contextId);
                }

                // Attempt to register. This will succeed if behaviors have already been set.
                RegisterBehaviorsWithService();
            }
        }

        /// <summary>
        /// A robust method to register behaviors with the InteractionService.
        /// It will only execute if all conditions are met: a valid context ID,
        /// a non-null InteractionService, and a pending registration flag.
        /// </summary>
        private void RegisterBehaviorsWithService()
        {
            // Do nothing if there's no new registration pending.
            if (!_behaviorsNeedRegistration)
                return;

            // Try to get the InteractionService. This will only succeed if ContextId is valid.
            _interactionService ??= GetService<InteractionService>();

            // If we couldn't get the service (because context is still not set), we just wait.
            // This method will be called again when SetContextId is called.
            if (_interactionService is null)
                return;

            var newBehaviours = _behaviours.Value;

            // Set the host and context on the new behaviors.
            foreach (var b in newBehaviours)
            {
                if (b is null)
                    continue;

                b.SetHost(this);
                b.SetContextId(this.ContextId);
            }

            // Perform the update.
            _interactionService.UpdateBehaviors(this, newBehaviours.Where(b => b is not null));

            // Mark registration as complete.
            _behaviorsNeedRegistration = false;
        }

        public virtual bool Notify(INotification notification, CallerInfo caller)
        {
            foreach (var child in Children.Reverse())
            {
                if (child is ILayer layer && layer.Notify(notification, caller))
                {
                    return true;
                }
            }

            if (_layer?.Notify(notification, caller) == true)
            {
                return true;
            }

            return false;
        }
    }
}
