using VL.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Core
{
    public abstract class AbstractGesture : ContextConsumer, IFuGesture
    {
        public abstract GestureState State { get; }

        protected AbstractGesture(NodeContext nodeContext)
            : base(nodeContext) { }
    }
}
