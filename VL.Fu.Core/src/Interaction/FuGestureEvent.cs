using VL.Fu.Core.Input;

namespace VL.Fu.Core.Interaction
{
    public record struct FuGestureEvent
    {
        public IFuGesture Gesture { get; init; }
        public object? Activator { get; init; }
        public FuInputState InputState { get; init; }

        public FuGestureEvent(IFuGesture gesture, object? activator, FuInputState inputState)
        {
            Gesture = gesture;
            Activator = activator;
            InputState = inputState;
        }
    }
}
