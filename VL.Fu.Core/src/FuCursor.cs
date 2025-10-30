using Stride.Core.Mathematics;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core
{
    public record struct FuCursor
    {
        public int Id { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Delta { get; set; }
        public TouchNotificationKind State { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public TimeSpan LifeSpan => DateTimeOffset.UtcNow - CreatedAt;

        public FuCursor() { }

        public FuCursor(int id, Vector2 position, TouchNotificationKind state)
        {
            Id = id;
            Position = position;
            State = state;
            Delta = Vector2.Zero;
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
