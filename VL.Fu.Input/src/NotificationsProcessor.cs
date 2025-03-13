using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;


namespace Fu.Input;



[ProcessNode()]
public class NotificationsProcessor
{
    public Spread<Cursor> Output { get; private set; } = Spread<Cursor>.Empty;

    private IObservable<INotification>? _source;

    private MouseState _mouseState;
    private Cursor? _mouseCursor;

    private bool _invalidate;


    private Dictionary<int, Cursor> _touchMessages = new Dictionary<int, Cursor>();

    private Dictionary<int, Cursor> _touchCursors = new Dictionary<int, Cursor>();

    public void Update(IObservable<INotification>? source)
    {
        if (_source != source)
        {
            _source = source;

            _source?.Subscribe(ProcessNotification);
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

            _touchCursors?.ForEach(x => builder.Add(x.Value));

            Output = builder.ToSpread();

            _invalidate = false;
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

    private void ProcessNotification(INotification n)
    {
        switch (n)
        {
            case MouseNotification mouseNotification:
                HandleMouseNotification(mouseNotification);
                break;
            case TouchNotification touchNotification:
                HandleTouchNotification(touchNotification);
                break;
        }
    }

    private void HandleMouseNotification(MouseNotification mouseNotification)
    {
        switch (mouseNotification.Kind)
        {
            case MouseNotificationKind.MouseUp:
                {
                    var n = mouseNotification as MouseDownNotification;
                    _mouseState = _mouseState with
                    {
                        IsLeft = n?.Buttons.IsLeft() ?? false,
                        IsRight = n?.Buttons.IsRight() ?? false,
                        IsMiddle = n?.Buttons.IsMiddle() ?? false,
                    };
                    break;
                }
            case MouseNotificationKind.MouseDown:
                {
                    var n = mouseNotification as MouseDownNotification;
                    _mouseState = _mouseState with
                    {
                        IsLeft = n?.Buttons.IsLeft() ?? false,
                        IsRight = n?.Buttons.IsRight() ?? false,
                        IsMiddle = n?.Buttons.IsMiddle() ?? false,
                    };
                    break;
                }
            case MouseNotificationKind.MouseMove:
                {
                    var n = mouseNotification as MouseMoveNotification;
                    _mouseState = _mouseState with
                    {
                        Position = n?.PositionInWorldSpace ?? Vector2.Zero,
                    };
                    break;
                }
            default:
                break;
        }
    }

    private void HandleTouchNotification(TouchNotification touchNotification)
    {
        if (touchNotification.Kind == TouchNotificationKind.TouchDown)
        {
            var cursor = new Cursor(touchNotification.Id, touchNotification.PositionInWorldSpace, Vector2.Zero, Enums.CursorState.Down, Enums.CursorSource.Touch);
            _touchMessages.Add(touchNotification.Id, cursor);

            return;
        }
        else if (touchNotification.Kind == TouchNotificationKind.TouchMove)
        {
            var cursor = new Cursor(touchNotification.Id, touchNotification.PositionInWorldSpace, Vector2.Zero, Enums.CursorState.Move, Enums.CursorSource.Touch);
            _touchMessages[touchNotification.Id] = cursor;

            return;
        }
        else
        {
            var cursor = new Cursor(touchNotification.Id, touchNotification.PositionInWorldSpace, Vector2.Zero, Enums.CursorState.Up, Enums.CursorSource.Touch);
            _touchMessages[touchNotification.Id] = cursor;

            return;
        }
    }



}
