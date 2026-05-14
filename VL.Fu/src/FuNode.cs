using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Lib.Collections;

namespace VL.Fu.Core
{
    [ProcessNode(HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public class FuNode : Interactable, IFuNode
    {
        [Fragment]
        public FuNode(NodeContext nodeContext)
            : base(nodeContext) { }

        private Spread<IFuNode>? _children;

        [Fragment(Order = PinOrder.Input)]
        public override void SetChildren(
            [Pin(PinGroupKind = Model.PinGroupKind.Collection, PinGroupDefaultCount = 1)]
                Spread<IFuNode> children
        )
        {
            if (ReferenceEquals(_children, children))
                return;

            _children = children;

            base.SetChildren(children);
        }
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

        private Spread<IFuNode>? _children;

        [Fragment(Order = PinOrder.Input)]
        public override void SetChildren(Spread<IFuNode> children)
        {
            if (ReferenceEquals(_children, children))
                return;

            _children = children;

            base.SetChildren(children);
        }
    }
}
