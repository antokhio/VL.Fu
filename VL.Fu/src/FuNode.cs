using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Lib.Collections;

namespace VL.Fu
{
    [ProcessNode(HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public class FuNode : Interactable, IFuNode
    {
        [Fragment]
        public FuNode(NodeContext nodeContext)
            : base(nodeContext) { }
    }

    [ProcessNode(
        Name = "FuNode (Spectral)",
        HasStateOutput = true,
        FragmentSelection = FragmentSelection.Explicit
    )]
    public class FuNodeSpectral : FuNode
    {
        [Fragment]
        public FuNodeSpectral(NodeContext nodeContext)
            : base(nodeContext) { }

        [Fragment(Order = PinOrder.Input)]
        public override void SetChildren(Spread<IFuNode> children)
        {
            base.SetChildren(children);
        }
    }
}
