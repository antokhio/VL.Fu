using Fu.Input.Core;
using Stride.Core.Mathematics;
using Stride.Input;

namespace Fu.Input;

public record struct Cursor(int Id, Vector2 Position, Vector2 Delta, CursorState State, CursorSource Source)
{
    public static Cursor DownCursor(MouseState mouseState) => new(-1, mouseState.Position, Vector2.Zero, CursorState.Down, CursorSource.Mouse);
    public static Cursor MoveCursor(MouseState mouseState, Vector2 delta) => new(-1, mouseState.Position, delta, CursorState.Move, CursorSource.Mouse);
    public static Cursor UpCursor(MouseState mouseState, Vector2 delta) => new(-1, mouseState.Position, delta, CursorState.Up, CursorSource.Mouse);
    public static Vector2 CalculateDelta(Vector2 currentPosition, Vector2 previousPosition) => previousPosition - currentPosition;


    public static Cursor FromMouseState(MouseState mouseState, CursorState state, Vector2 delta) => new()
    {
        Id = -1,
        Position = mouseState.Position,
        Delta = delta,
        State = state,
        Source = CursorSource.Mouse
    };

    public static Cursor FromPointerEvent(PointerEvent pointerEvent) => new()
    {
        Id = pointerEvent.PointerId,
        Position = pointerEvent.Position * pointerEvent.Pointer.SurfaceSize * 0.01f,
        Delta = pointerEvent.DeltaPosition * pointerEvent.Pointer.SurfaceSize * 0.01f,
        State =
        pointerEvent.EventType == PointerEventType.Pressed
        ? CursorState.Down
        : pointerEvent.EventType == PointerEventType.Released
        || pointerEvent.EventType == PointerEventType.Canceled
        ? CursorState.Up
        : CursorState.Move,
        Source = CursorSource.Touch
    };
};


