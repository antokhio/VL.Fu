using VL.Core;
using VL.Core.Import;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Interactable : HitTestable, IBehavior
    {
        [Fragment]
        protected Interactable(NodeContext nodeContext)
            : base(nodeContext) { }

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
