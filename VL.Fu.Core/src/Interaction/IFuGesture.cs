using VL.Fu.Core.Input;

namespace VL.Fu.Core.Interaction
{
    public interface IFuGesture : IContextConsumer
    {
        // The last reported phase (internal state)
        GesturePhase Phase { get; }

        // Detection Entry Point
        FuGestureState Match(IFuNode host, FuInputState inputState);

        // Update Step
        FuGestureState Advance(IFuNode host, FuInputState inputState);

        void Reset();
    }
}
