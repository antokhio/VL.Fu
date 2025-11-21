using Stride.Core.Mathematics;
using VL.Fu.Core.Common;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Input
{
    public record struct FuPointer
    {
        public int Id { get; init; }
        public Vector2 Position { get; init; }
        public Vector2 Delta { get; init; } = Vector2.Zero;
        public TouchNotificationKind State { get; init; }
        public DateTimeOffset TimeStamp { get; init; } = DateTimeOffset.UtcNow;
        public TimeSpan Lifetime => DateTimeOffset.UtcNow - TimeStamp;

        public bool IsLeft { get; init; }
        public bool IsRight { get; init; }
        public bool IsMiddel { get; init; }
        public bool IsPressed => IsLeft || IsRight || IsMiddel;

        public FuPointer(int id, Vector2 position, TouchNotificationKind state)
        {
            Id = id;
            Position = position;
            State = state;
        }

        public static readonly FuPointer DefaultMousePointer = new FuPointer()
        {
            Id = Constants.MousePointerId,
        };

        public FuPointer WithMouseState(FuMouse mouseState, TouchNotificationKind pointerState) =>
            this with
            {
                Position = mouseState.Position,
                IsLeft = mouseState.IsLeft,
                IsRight = mouseState.IsRight,
                IsMiddel = mouseState.IsMiddle,
                State = pointerState,
                Delta = pointerState == TouchNotificationKind.TouchDown ? Delta : Vector2.Zero,
            };

        public FuPointer WithState(TouchNotificationKind newState) =>
            this with
            {
                State = newState,
            };

        public FuPointer WithPosition(Vector2 position) =>
            this with
            {
                Position = position,
                Delta = position - this.Position,
            };

        public bool IsMousePointer() => Id == Constants.MousePointerId;
    }
}
