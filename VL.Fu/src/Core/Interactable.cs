using System.Collections.Immutable;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Property;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Interactable : HitTestable, IBehavior, IInteractable
    {
        private readonly CachedProperty<Spread<IFuBehaviour>> _behaviours = new(
            Spread<IFuBehaviour>.Empty
        );

        public IReadOnlyList<IFuBehaviour> Behaviours { get; private set; } =
            ImmutableList<IFuBehaviour>.Empty;

        [Fragment]
        protected Interactable(NodeContext nodeContext)
            : base(nodeContext) { }

        [Fragment]
        public void SetBehaviours(Spread<IFuBehaviour> behaviours) =>
            _behaviours.TrySetValue(
                behaviours,
                (curr, next) =>
                {
                    Behaviours = next.Where(b => b != null)
                        .OrderByDescending(b => b.Priority)
                        .ToImmutableList();
                }
            );

        // Implements vvvv IBehaviour for convinence
        public virtual bool Notify(INotification notification, CallerInfo caller)
        {
            foreach (var child in Children.Reverse())
            {
                if (child is ILayer layer && layer.Notify(notification, caller))
                    return true;
            }
            return _layer?.Notify(notification, caller) == true;
        }
    }
}
