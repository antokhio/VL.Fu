using Stride.Input;
using VL.Core.Import;
using VL.Lib.Collections;


namespace Fu.Input;

[ProcessNode()]
public class PointerEventsProcessor : InputProcessorBase
{
    public Spread<Cursor> Output { get; protected set; } = Spread<Cursor>.Empty;
    public void Update(IReadOnlyList<InputEvent> input)
    {
        foreach (var inputEvent in input)
        {
            if (inputEvent is PointerEvent pointerEvent)
            {
                if (pointerEvent.Device is IMouseDevice mouseDevice)
                {
                    _mouseState = _mouseState.FromMouseDevice(mouseDevice);
                }
                else
                {
                    var cursor = Cursor.FromPointerEvent(pointerEvent);
                    _touchMessages[cursor.Id] = cursor;
                }
            }
        }

        HandleMouse();
        HandleTouch();

        if (_invalidate)
        {
            var builder = new SpreadBuilder<Cursor>();

            if (_mouseCursor != null)
            {
                builder.Add(_mouseCursor);
            }

            _touchCursors.ForEach(x => builder.Add(x.Value));

            Output = builder.ToSpread();

            _invalidate = false;
        }
    }
}