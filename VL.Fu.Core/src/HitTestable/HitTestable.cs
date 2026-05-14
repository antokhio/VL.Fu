using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.HitTest;
using VL.Fu.Core.Input;
using VL.Fu.Core.Property;
using VL.Fu.Core.Selection;

namespace VL.Fu.Core
{
    /// <summary>
    /// A base class that extends rendering capabilities with interaction testing configuration.
    /// The actual execution of the tests is deferred to the concrete implementation which satisfies IFuNode.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class HitTestable : Renderable, IHitTestProvider, IAreaTestProvider
    {
        protected static readonly IHitTest DefaultHitTestBounds = new HitTestBounds();
        protected static readonly IAreaTest DefaultAreaTestBounds = new AreaTestBounds();

        protected readonly CachedProperty<IHitTest> _hitTest = new(DefaultHitTestBounds);
        protected readonly CachedProperty<IAreaTest> _areaTest = new(DefaultAreaTestBounds);

        protected HitTestable(NodeContext nodeContext)
            : base(nodeContext) { }

        [Fragment(Order = PinOrder.HitTest)]
        public void SetHitTest(
            [Pin(Visibility = Model.PinVisibility.Optional)] IHitTest? hitTest
        ) => _hitTest.SetValue(hitTest ?? DefaultHitTestBounds);

        [Fragment(Order = PinOrder.AreaTest)]
        public void SetAreaTest(
            [Pin(Visibility = Model.PinVisibility.Optional)] IAreaTest? areaTest
        ) => _areaTest.SetValue(areaTest ?? DefaultAreaTestBounds);

        public bool HitTest(FuPointer pointer)
        {
            return _hitTest.Value.HitTest((IFuNode)this, pointer);
        }

        public bool IsContainedIn(ISelectionShape shape)
        {
            return _areaTest.Value.IsContainedIn((IFuNode)this, shape);
        }
    }
}
