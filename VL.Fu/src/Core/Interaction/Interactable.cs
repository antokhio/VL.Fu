using VL.Fu.Core.Layout;
using VL.Core;
using VL.Core.Import;
using VL.Lib.IO.Notifications;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    public interface IInteractable : IBehavior, ILayoutable { }

    public abstract class Interactable<TSelf> : Layoutable<TSelf>, IInteractable
        where TSelf : Interactable<TSelf>
    {
        protected Interactable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            throw new NotImplementedException();
        }
    }
}
