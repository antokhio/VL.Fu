using VL.Fu.Core.Common;
using VL.Fu.Core.Input;

namespace VL.Fu.Core.Interaction
{
    public interface IFuGesture : IContextConsumer
    {
        GestureState State { get; }

        /// <summary>
        /// Evaluates the input state against a specific host node to determine if the gesture should start.
        /// </summary>
        public bool Match(
            IFuNode host,
            FuInputState inputState,
            out FuGestureEvent? gestureMatchedEvent
        );

        /// <summary>
        /// Updates the state of a running gesture based on new input and the host context.
        /// </summary>
        public bool Advance(
            IFuNode host,
            FuInputState inputState,
            out FuGestureEvent? gestureAdvancedEvent
        );
        public void Reset();
    }
}
