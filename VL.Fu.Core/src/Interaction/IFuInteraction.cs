namespace VL.Fu.Core.Interaction
{
    public readonly record struct IFuInteraction
    {
        object? Caller { get; init; } // Pointer or other object that initiated the interaction
        IFuBehaviour Behaviour { get; init; }
        IFuGesture Gesture { get; init; }
        IFuNode Host { get; init; }

        public IFuInteraction(
            object? caller,
            IFuBehaviour behaviour,
            IFuGesture gesture,
            IFuNode host
        )
        {
            Caller = caller;
            Behaviour = behaviour;
            Gesture = gesture;
            Host = host;
        }
    }
}
