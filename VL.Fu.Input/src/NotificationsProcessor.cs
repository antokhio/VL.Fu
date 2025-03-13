using Fu.Input.Core;
using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;


namespace Fu.Input;

[ProcessNode()]
public class NotificationsProcessor : InputProcessorBase
{
    public Spread<Cursor> Output { get; protected set; } = Spread<Cursor>.Empty;

    private IObservable<INotification>? _source;

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
            var cursor = new Cursor(touchNotification.Id, touchNotification.PositionInWorldSpace, Vector2.Zero, CursorState.Down, CursorSource.Touch);
            _touchMessages.Add(touchNotification.Id, cursor);

            return;
        }
        else if (touchNotification.Kind == TouchNotificationKind.TouchMove)
        {
            var cursor = new Cursor(touchNotification.Id, touchNotification.PositionInWorldSpace, Vector2.Zero, CursorState.Move, CursorSource.Touch);
            _touchMessages[touchNotification.Id] = cursor;

            return;
        }
        else
        {
            var cursor = new Cursor(touchNotification.Id, touchNotification.PositionInWorldSpace, Vector2.Zero, CursorState.Up, CursorSource.Touch);
            _touchMessages[touchNotification.Id] = cursor;

            return;
        }
    }



}
