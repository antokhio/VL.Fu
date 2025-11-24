using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;

namespace VL.Fu
{
    [ProcessNode(HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public class FuNode : ContextConsumer
    {
        [Fragment]
        public FuNode(NodeContext nodeContext)
            : base(nodeContext) { }
    }
}
