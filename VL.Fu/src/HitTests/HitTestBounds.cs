using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.HitTest;
using VL.Fu.Core.Input;

namespace VL.Fu.HitTests
{
    [ProcessNode(Name = "HitTest (Bounds)", FragmentSelection = FragmentSelection.Explicit)]
    public class HitTestBounds : OutputBase<IHitTest>, IHitTest
    {
        [Fragment]
        public HitTestBounds() { }

        public bool HitTest(IFuNode node, FuPointer pointer)
        {
            return HitTestHelper.RectangleHitTest(node.Bounds, pointer);
        }
    }
}
