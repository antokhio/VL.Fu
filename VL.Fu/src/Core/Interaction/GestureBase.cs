using VL.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;

namespace VL.Fu.Core.Interaction
{
    public abstract class GestureBase : ContextConsumer, IFuGesture
    {
        public GestureState State { get; protected set; } = GestureState.Ready;

        protected GestureBase(NodeContext nodeContext)
            : base(nodeContext) { }

        public abstract bool Match(
            IFuNode host,
            FuInputState inputState,
            out FuGestureEvent? gestureMatchedEvent
        );

        public abstract bool Advance(
            IFuNode host,
            FuInputState inputState,
            out FuGestureEvent? gestureAdvancedEvent
        );

        public virtual void Reset()
        {
            State = GestureState.Ready;
        }
    }
}
