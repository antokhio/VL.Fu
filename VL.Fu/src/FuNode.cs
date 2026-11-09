using VL.Fu.Core.Region;
using VL.Core;
using VL.Core.Import;
using VL.Core.PublicAPI;
using VL.Model;

namespace VL.Fu
{
    [ProcessNode(Name = "FuNode", HasStateOutput = true)]
    [Region(SupportedBorderControlPoints = ControlPointType.None)]
    public class FuNode : RegionProvider
    {
        public FuNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }
    }
}
