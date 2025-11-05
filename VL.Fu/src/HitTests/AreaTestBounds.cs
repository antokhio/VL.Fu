using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.HitTest;

namespace VL.Fu.HitTests
{
    [ProcessNode(Name = "AreaTest (Bounds)", FragmentSelection = FragmentSelection.Explicit)]
    public class AreaTestBounds : OutputBase<IAreaTest>, IAreaTest
    {
        [Fragment]
        public AreaTestBounds() { }

        public bool IsContainedIn(IFuNode node, RectangleF area)
        {
            return HitTestHelper.RectangleContainedInRectangle(node.Bounds, area);
        }
    }
}
