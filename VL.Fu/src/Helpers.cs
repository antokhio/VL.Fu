using VL.Fu.Core.HitTest;

namespace VL.Fu
{
    public static class Helpers
    {
        public static class HitTest
        {
            public static readonly IHitTest HitTestBounds = new HitTestBounds();
        }

        public static class AreaTest
        {
            public static readonly IAreaTest AreaTestBounds = new AreaTestBounds();
        }
    }
}
