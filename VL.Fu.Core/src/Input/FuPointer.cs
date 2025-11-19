using Stride.Core.Mathematics;
using VL.Fu.Core.Common;
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

        public FuPointer WithState(TouchNotificationKind newState) =>
            this with
            {
                State = newState,
            };

        public FuPointer WithPosition(Vector2 newPosition) =>
            this with
            {
                Position = newPosition,
                Delta = newPosition - this.Position,
            };

        public bool IsMousePointer() => Id == Constants.MousePointerId;
    }
}
