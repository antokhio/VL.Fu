using VL.Core.Import;
using VL.Lib.Collections;

namespace VL.Fu.Styles
{
    public record XXX { }

    [ProcessNode]
    public abstract class TesterNode
    {
        public void SetGroup(
            [Pin(PinGroupKind = VL.Model.PinGroupKind.Collection, PinGroupDefaultCount = 1)]
                Spread<XXX> Test
        ) { }
    }

    [ProcessNode]
    public class TestXNode : TesterNode { }
}
