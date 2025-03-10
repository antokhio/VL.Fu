using Fu.Input.Core.Enums;
using Stride.Core.Mathematics;

namespace Fu.Input.Core;

public record struct Cursor(Vector2 Position, int Id, CursorState State, CursorSource Source)
{
};
