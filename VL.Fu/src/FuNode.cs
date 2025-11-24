using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.HitTest;
using VL.Fu.Core.Input;

namespace VL.Fu
{
    [ProcessNode(HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public class FuNode : HitTestable, IFuNode
    {
        [Fragment]
        public FuNode(NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// Executes the hit test using the configured strategy.
        /// </summary>
        public override bool HitTest(FuPointer pointer)
        {
            // 'this' is passed safely because FuNode implements IFuNode.
            return _hitTest.Value.HitTest(this, pointer);
        }

        /// <summary>
        /// Executes the area test using the configured strategy.
        /// </summary>
        public override bool IsContainedIn(ISelectionShape shape)
        {
            // 'this' is passed safely because FuNode implements IFuNode.
            return _areaTest.Value.IsContainedIn(this, shape);
        }
    }
}
