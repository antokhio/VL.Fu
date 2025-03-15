using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Lib.Collections;

namespace Fu;

[ProcessNode()]
public class FuNode
{
    private RectangleF _layout;
    public void SetLayout(RectangleF layout)
    {
        if (_layout != layout)
        {
            _layout = layout;
        }
    }

    private Spread<FuNode?>? _children;
    public void SetChildren(Spread<FuNode?>? children)
    {
        if (_children != children)
        {
            _children = children;
        }
    }



}
