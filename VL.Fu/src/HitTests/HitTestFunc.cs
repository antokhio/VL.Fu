using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.HitTest;
using VL.Fu.Core.Input;
using VL.Fu.Core.Property;

namespace VL.Fu.HitTests
{
    public class HitTestFunc : OutputBase<IHitTest>, IHitTest
    {
        private readonly CachedProperty<Func<IFuNode, FuPointer, bool>?> _hitTestFunc = new(null);

        [Fragment(Order = PinOrder.Input)]
        public void SetHitTest(Func<IFuNode, FuPointer, bool>? hitTestFunc)
        {
            _hitTestFunc.TrySetValue(hitTestFunc);
        }

        public bool HitTest(IFuNode node, FuPointer pointer)
        {
            return _hitTestFunc.Value?.Invoke(node, pointer)
                ?? HitTestHelper.RectangleHitTest(node.Bounds, pointer);
        }
    }
}
