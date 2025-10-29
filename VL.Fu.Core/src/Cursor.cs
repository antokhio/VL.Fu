using Stride.Core.Mathematics;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core
{
    public record struct Cursor
    {
        public enum CursorState
        {
            Down,
            Move,
            Up,
        }

        public int Id { get; set; }
        public Vector2 Position { get; set; }
        public TouchNotificationKind State { get; set; }

        public Cursor() { }

        public Cursor(int id, Vector2 position, TouchNotificationKind state)
        {
            Id = id;
            Position = position;
            State = state;
        }
    }
}
