using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Layout;
using VL.Fu.Core.Tree;
using VL.Lib.IO.Notifications;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    public interface IInteractable : ILayoutable, IBehavior { }

    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Interactable<TSelf> : Layoutable<TSelf>, IInteractable
        where TSelf : class, ITraversable<TSelf>, IInteractable
    {
        protected Interactable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            //throw new NotImplementedException();
            return false;
        }
    }
}
