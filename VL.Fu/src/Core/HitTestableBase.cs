using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.HitTest;
using VL.Fu.Core.Input;
using VL.Fu.Core.Property;
using VL.Fu.HitTests;

namespace VL.Fu.Core
{
    /// <summary>
    /// An abstract base class that adds spatial testing capabilities to a RenderingBase.
    /// It provides default implementations for hit-testing and area-testing based on the node's bounds.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class HitTestableBase : RenderingBase, IHitTestProvider, IAreaTestProvider
    {
        private readonly CachedProperty<IHitTest> _hitTest = new(new HitTestBounds());
        private readonly CachedProperty<IAreaTest> _areaTest = new(new AreaTestBounds());

        /// <summary>
        /// Provides a custom object to handle hit-testing logic.
        /// </summary>
        [Fragment(Order = PinOrder.HitTest)]
        public void SetHitTest([Pin(Visibility = Model.PinVisibility.Optional)] IHitTest? hitTest)
        {
            _hitTest.TrySetValue(hitTest ?? new HitTestBounds());
        }

        /// <summary>
        /// Provides a custom object to handle area-testing logic.
        /// </summary>
        [Fragment(Order = PinOrder.AreaTest)]
        public void SetAreaTest(
            [Pin(Visibility = Model.PinVisibility.Optional)] IAreaTest? areaTest
        )
        {
            _areaTest.TrySetValue(areaTest ?? new AreaTestBounds());
        }

        /// <summary>
        /// Performs a hit test by delegating to the current IHitTest object.
        /// </summary>
        public bool HitTest(FuPointer pointer)
        {
            return _hitTest.Value.HitTest((IFuNode)this, pointer);
        }

        /// <summary>
        /// Determines if the node is contained within an area by delegating to the current IAreaTest object.
        /// </summary>
        public bool IsContainedIn(RectangleF area)
        {
            return _areaTest.Value.IsContainedIn((IFuNode)this, area);
        }
    }
}
