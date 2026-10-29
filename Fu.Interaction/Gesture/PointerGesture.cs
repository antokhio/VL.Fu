namespace Fu.Interaction.Gesture
{
    public record class PointerGesture : Gesture
    {
        public int? PointerId { get; init; }
    }

    public record class PointerPressedGesture : PointerGesture { }
}
