using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core
{
    /// <summary>
    /// A composable base class that adds notification-handling behaviors to a RenderingBase.
    /// It orchestrates notifications between dedicated behaviors, child nodes, and a primary layer.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class BehavioralBase : HitTestableBase, IBehavior
    {
        private readonly CachedProperty<Spread<IFuBehaviour>> _behaviours = new(
            Spread<IFuBehaviour>.Empty
        );

        /// <summary>
        /// Sets a spread of composable behaviors to handle notifications.
        /// This operation is optimized to only update context IDs when the spread changes.
        /// </summary>
        [Fragment(Order = PinOrder.Behaviour)]
        public void SetBehaviours(Spread<IFuBehaviour> behaviours)
        {
            _behaviours.TrySetValue(
                behaviours,
                (oldSpread, newSpread) =>
                {
                    // This logic now only runs when the spread has actually changed.
                    foreach (var b in newSpread)
                    {
                        b.SetContextId(this.ContextId);
                    }
                }
            );
        }

        /// <summary>
        /// Handles notifications by forwarding them to children first (in reverse rendering order),
        /// and then to the primary layer if no child handled the event. This ensures the top-most
        /// visual element gets the first chance to react.
        /// </summary>
        public virtual bool Notify(INotification notification, CallerInfo caller)
        {
            // 1. Iterate through children in reverse order.
            // The last child drawn is visually on top, so it gets the first chance to handle the notification.
            foreach (var child in Children.Reverse())
            {
                if (child is ILayer layer && layer.Notify(notification, caller))
                {
                    return true;
                }
            }

            // 2. If no child handled the notification, try the primary `_layer`.
            if (_layer?.Notify(notification, caller) == true)
            {
                return true;
            }

            // 3. If nobody handled it, return false.
            return false;
        }
    }
}
