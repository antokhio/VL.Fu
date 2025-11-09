using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Lib.Collections;

namespace VL.Fu
{
    [ProcessNode(Name = "FuNode", FragmentSelection = FragmentSelection.Explicit)]
    public class FuNode : InteractiveHost, IFuNode
    {
        [Fragment]
        public FuNode() { }

        [Fragment(Order = PinOrder.Output)]
        public IFuNode Ouput => this;
    }

    [ProcessNode(Name = "FuNode (Spectral)", FragmentSelection = FragmentSelection.Explicit)]
    public class FuNodeSpectral : FuNode
    {
        [Fragment]
        public FuNodeSpectral() { }

        public override void SetInput(Spread<IFuNode> input)
        {
            base.SetInput(input);
        }
    }
}
