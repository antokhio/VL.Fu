using VL.Core;
using VL.Fu.Core.Input;

namespace VL.Fu.Core.Interaction
{
    public abstract class GestureBase : ContextConsumer, IFuGesture
    {
        public GesturePhase Phase { get; protected set; } = GesturePhase.Idle;

        protected GestureBase(NodeContext nodeContext)
            : base(nodeContext) { }

        public abstract FuGestureState Match(IFuNode host, FuInputState inputState);

        public abstract FuGestureState Advance(IFuNode host, FuInputState inputState);

        public virtual void Reset()
        {
            Phase = GesturePhase.Idle;
        }
    }
}
