using Stride.Core.Mathematics;
using Stride.Input;
using VL.Core.Import;
using VL.Lib.Collections;


namespace Fu.Input.Core;

[ProcessNode()]
public class PointerEventsProcessor
{
    public Spread<Cursor> Output { get; private set; } = Spread<Cursor>.Empty;

    private MouseState _mouseState;
    private Cursor? _mouseCursor;

    private Dictionary<int, Cursor> _touchMessages = new Dictionary<int, Cursor>();
    private Dictionary<int, Cursor> _touchCursors = new Dictionary<int, Cursor>();

    private bool _invalidate;

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

    private void HandleTouch()
    {
        foreach (var touch in _touchCursors)
        {
            if (touch.Value.State == Enums.CursorState.Up)
            {
                _touchCursors.Remove(touch.Key);
                _invalidate = true;
            }
        }

        if (_touchMessages.Count > 0)
        {
            foreach (var touch in _touchMessages)
            {
                if (touch.Value.State == Enums.CursorState.Down)
                {
                    _touchCursors.Add(touch.Key, touch.Value with { Delta = Vector2.Zero });
                }
                else if (touch.Value.State == Enums.CursorState.Move || touch.Value.State == Enums.CursorState.Up)
                {
                    var hasPreviousCursor = _touchCursors.TryGetValue(touch.Key, out var previousCursor);
                    _touchCursors[touch.Key] = hasPreviousCursor
                        ? touch.Value with { Delta = Cursor.CalculateDelta(touch.Value.Position, previousCursor.Position) }
                        : touch.Value with { Delta = Vector2.Zero, State = Enums.CursorState.Down };
                }
            }

            _touchMessages.Clear();
            _invalidate = true;
        }
    }

    private void HandleMouse()
    {
        if (_mouseState.IsLeft)
        {
            if (_mouseCursor == null)
            {
                _mouseCursor = Cursor.DownCursor(_mouseState);
            }
            else
            {
                _mouseCursor = Cursor.MoveCursor(_mouseState, Cursor.CalculateDelta(_mouseState.Position, _mouseCursor.Value.Position));

            }
            _invalidate = true;

        }
        else if (_mouseCursor != null)
        {
            if (_mouseCursor.Value.State == Enums.CursorState.Move)
            {
                _mouseCursor = Cursor.UpCursor(_mouseState, _mouseState.Position - _mouseCursor.Value.Position);
            }
            else
            {
                _mouseCursor = null;
            }
            _invalidate = true;
        }
    }
}