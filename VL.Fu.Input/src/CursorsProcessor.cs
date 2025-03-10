using Fu.Input.Core.Enums;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Lib.Reactive;

namespace Fu.Input.Core;

[ProcessNode()]
public class CursorsProcessor
{
    private IChannel<Spread<Cursor>> _cursors = Channel.Create(Spread<Cursor>.Empty);
    public IChannel<Spread<Cursor>> Cursors
    {
        get => _cursors;
    }

    private bool _isMouseDown;

    public void Update(Spread<Cursor>? input)
    {
        if (IsTouchInput(input))
        {
            _isMouseDown = false;

            var cursors = input.Where(x => x.Source == CursorSource.Touch);

            var dict = new Dictionary<int, Cursor>();

            foreach (var cursor in cursors)
            {
                if (dict.ContainsKey(cursor.Id))
                {
                    dict[cursor.Id] = cursor;
                }
                else
                {
                    dict.Add(cursor.Id, cursor);
                }

            }

            Cursors.OnNext(dict.Select(x => x.Value).ToSpread());
        }
        else if (IsMouseInput(input))
        {
            var isDown = input.Any(x => x.State == CursorState.Down);
            var isUp = input.Any(x => x.State == CursorState.Up);

            var cursors = input.Where(x => x.Source == CursorSource.Mouse);

            if (isDown)
            {
                _isMouseDown = true;
            }
            if (isUp || cursors.Count() == 0)
            {
                _isMouseDown = false;
            }


            if (_isMouseDown)
            {
                Cursors.OnNext(Spread.Create(cursors.FirstOrDefault()));
            }
            else
            {
                Cursors.OnNext(Spread<Cursor>.Empty);
            }
        }
        else
        {
            if (Cursors.Value != Spread<Cursor>.Empty)
            {
                Cursors.OnNext(Spread<Cursor>.Empty);
            }
        }
    }

    public static bool IsTouchInput(Spread<Cursor>? input) => input?.Any(x => x.Source == Enums.CursorSource.Touch) ?? false;
    public static bool IsMouseInput(Spread<Cursor>? input) => input?.Any(x => x.Source == Enums.CursorSource.Mouse) ?? false;
}
