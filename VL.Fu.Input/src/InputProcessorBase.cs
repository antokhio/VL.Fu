using Fu.Input.Core;
using Stride.Core.Mathematics;

namespace Fu.Input;

public abstract class InputProcessorBase
{
    protected MouseState _mouseState;
    protected Cursor? _mouseCursor;

    protected Dictionary<int, Cursor> _touchMessages = new Dictionary<int, Cursor>();
    protected Dictionary<int, Cursor> _touchCursors = new Dictionary<int, Cursor>();

    protected bool _invalidate;
    protected void HandleMouse()
    {
        if (_mouseState.IsLeft)
        {
            if (_mouseCursor == null)
            {
                _mouseCursor = Cursor.FromMouseState(_mouseState, CursorState.Down, Vector2.Zero);
            }
            else
            {
                _mouseCursor = Cursor.FromMouseState(_mouseState, CursorState.Move, Cursor.CalculateDelta(_mouseState.Position, _mouseCursor.Value.Position));
            }
            _invalidate = true;

        }
        else if (_mouseCursor != null)
        {
            if (_mouseCursor.Value.State == CursorState.Move)
            {
                _mouseCursor = Cursor.FromMouseState(_mouseState, CursorState.Up, Cursor.CalculateDelta(_mouseState.Position, _mouseCursor.Value.Position));
            }
            else
            {
                _mouseCursor = null;
            }
            _invalidate = true;
        }
    }

    protected void HandleTouch()
    {
        foreach (var touch in _touchCursors)
        {
            if (touch.Value.State == CursorState.Up)
            {
                _touchCursors.Remove(touch.Key);
                _invalidate = true;
            }
        }

        if (_touchMessages.Count > 0)
        {
            foreach (var touch in _touchMessages)
            {
                if (touch.Value.State == CursorState.Down)
                {
                    _touchCursors[touch.Key] = touch.Value with { Delta = Vector2.Zero };
                }
                else if (touch.Value.State == CursorState.Move || touch.Value.State == CursorState.Up)
                {
                    var hasPreviousCursor = _touchCursors.TryGetValue(touch.Key, out var previousCursor);
                    _touchCursors[touch.Key] = hasPreviousCursor
                        ? touch.Value with { Delta = Cursor.CalculateDelta(touch.Value.Position, previousCursor.Position) }
                        : touch.Value with { Delta = Vector2.Zero, State = CursorState.Down };
                }
            }

            _touchMessages.Clear();
            _invalidate = true;
        }
    }
}
