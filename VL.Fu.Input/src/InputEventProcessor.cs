using Fu.Input.Core.Enums;
using Stride.Core.Mathematics;
using Stride.Input;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Skia;


namespace Fu.Input.Core;

[ProcessNode()]
public class InputEventProcessor
{

    public void Update(IReadOnlyList<InputEvent> input, out Spread<Cursor> output, CommonSpace space)
    {
        var builder = new SpreadBuilder<Cursor>();

        foreach (var inputEvent in input)
        {
            if (inputEvent is PointerEvent pointerEvent)
            {
                builder.Add(ToCursor(pointerEvent));
            }
        }

        output = builder.ToSpread();
    }


    public static CursorState ToCursorState(PointerEventType input)
    {
        switch (input)
        {
            case PointerEventType.Pressed:
                return CursorState.Down;
            case PointerEventType.Moved:
                return CursorState.Move;
            case PointerEventType.Canceled:
            case PointerEventType.Released:
                return CursorState.Up;
            default:
                throw new NotImplementedException();
        }
    }

    public static CursorSource ToCursorSource(IInputDevice input)
    {
        switch (input)
        {
            case IMouseDevice:
                return CursorSource.Mouse;
            case IPointerDevice:
                return CursorSource.Touch;
            default:
                throw new NotImplementedException();

        }
    }

    public static Cursor ToCursor(PointerEvent input) => new Cursor(
       new Vector2(input.AbsolutePosition.X * 0.01f, input.AbsolutePosition.Y * 0.01f), input.PointerId, ToCursorState(input.EventType), ToCursorSource(input.Device));
}