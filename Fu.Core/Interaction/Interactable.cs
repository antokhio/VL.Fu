using Fu.Core.Layout;
using VL.Core;
using VL.Core.Import;
using VL.Lib.IO.Notifications;
using VL.Model;
using VL.Skia;

namespace Fu.Core.Interaction
{
    public abstract class Interactable<TSelf> : Layoutable<TSelf>, IBehavior
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
