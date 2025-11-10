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

        /// <summary>
        /// Sets the spread of composable behaviors to be hosted by this node.
        /// </summary>
        [Fragment(Order = PinOrder.Behaviour)]
        public void SetBehaviours(Spread<IInteractiveBehavior> behaviours)
        {
            _behaviours.TrySetValue(behaviours, OnBehavioursChanged);
        }

        private void OnBehavioursChanged(
            Spread<IInteractiveBehavior> oldBehaviours,
            Spread<IInteractiveBehavior> newBehaviours
        )
        {
            _interactionService ??= GetService<InteractionService>();
            if (_interactionService is null)
                return;

            foreach (var b in newBehaviours)
            {
                b?.SetContextId(this.ContextId);
            }

            _interactionService.UpdateBehaviors(this, newBehaviours.Where(b => b is not null));
        }

        public override void SetContextId(int contextId)
        {
            if (contextId == this.ContextId)
                return;

            base.SetContextId(contextId);

            foreach (var behaviour in _behaviours.Value)
            {
                behaviour?.SetContextId(contextId);
            }
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
