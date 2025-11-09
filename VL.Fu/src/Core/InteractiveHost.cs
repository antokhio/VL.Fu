using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
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
        private readonly CachedProperty<Spread<IFuBehaviour>> _behaviours = new(
            Spread<IFuBehaviour>.Empty
        );

        private InteractionService? _interactionService;

        /// <summary>
        /// Sets the spread of composable behaviors to be hosted by this node.
        /// </summary>
        [Fragment(Order = PinOrder.Behaviour)]
        public void SetBehaviours(Spread<IFuBehaviour> behaviours)
        {
            _behaviours.TrySetValue(behaviours, OnBehavioursChanged);
        }

        private void OnBehavioursChanged(
            Spread<IFuBehaviour> oldBehaviours,
            Spread<IFuBehaviour> newBehaviours
        )
        {
            _interactionService ??= GetService<InteractionService>();
            if (_interactionService is null)
                return;

            // Propagate context ID to new behaviors, handling potential nulls.
            foreach (var b in newBehaviours)
            {
                b?.SetContextId(this.ContextId);
            }

            // Tell the service to update its state for this host.
            // We filter out nulls before passing them to the service.
            _interactionService.UpdateBehaviors(this, newBehaviours.Where(b => b is not null));
        }

        /// <summary>
        /// Overrides SetContextId to propagate the ID to children and attached behaviors.
        /// </summary>
        public override void SetContextId(int contextId)
        {
            if (contextId == this.ContextId)
                return;

            base.SetContextId(contextId);

            // Push the new context ID to all existing behaviors, handling potential nulls.
            foreach (var behaviour in _behaviours.Value)
            {
                behaviour?.SetContextId(contextId);
            }
        }

        /// <summary>
        /// Handles the Skia ILayer notification chain.
        /// </summary>
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

        // Note: A robust Dispose implementation would be needed here to unregister behaviors
        // when the node is removed. For example:
        // public override void Dispose()
        // {
        //      _interactionService ??= GetService<InteractionService>();
        //      _interactionService?.UpdateBehaviors(this, Enumerable.Empty<IFuBehaviour>());
        //      base.Dispose();
        // }
    }
}
