using Fu.Core.Rendering;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace VL.Fu
{
    [ProcessNode(Name = "FuNode", HasStateOutput = true)]
    public class FuNode : Renderable
    {
        public FuNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }
    }
}
