using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.HitTest;
using VL.Fu.Core.Property;

namespace VL.Fu.HitTests
{
    [ProcessNode(Name = "AreaTest (Func)", FragmentSelection = FragmentSelection.Explicit)]
    public class AreaTestFunc : OutputBase<IAreaTest>, IAreaTest
    {
        private readonly CachedProperty<Func<IFuNode, RectangleF, bool>?> _areaTestFunc = new(null);

        [Fragment(Order = PinOrder.Input)]
        public void SetAreaTest(Func<IFuNode, RectangleF, bool>? areaTestFunc)
        {
            _areaTestFunc.TrySetValue(areaTestFunc);
        }

        [Fragment]
        public AreaTestFunc() { }

        public bool IsContainedIn(IFuNode node, RectangleF area)
        {
            return _areaTestFunc.Value?.Invoke(node, area)
                ?? HitTestHelper.RectangleContainedInRectangle(node.Bounds, area);
        }
    }
}
