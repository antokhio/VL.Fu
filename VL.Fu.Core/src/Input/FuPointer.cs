using Stride.Core.Mathematics;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Input
{
    public record struct FuPointer
    {
        public int Id { get; init; }
        public Vector2 Position { get; init; }
        public Vector2 Delta { get; init; }
        public TouchNotificationKind State { get; init; }
        public DateTimeOffset TimeStamp { get; init; }

        public static readonly FuPointer Default = new FuPointer();

        public FuPointer WithState(TouchNotificationKind newState)
        {
            return this with { State = newState };
        }

        public FuPointer WithPosition(Vector2 newPosition)
        {
            return this with { Position = newPosition, Delta = newPosition - this.Position };
        }
    }
}
