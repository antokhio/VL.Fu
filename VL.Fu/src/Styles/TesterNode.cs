using VL.Core.Import;
using VL.Fu.Core.Styling;
using VL.Lib.Collections;

namespace VL.Fu.Styles
{
    [ProcessNode]
    public abstract class TesterNode
    {
        public void SetGroup(
            [Pin(PinGroupKind = VL.Model.PinGroupKind.Collection, PinGroupDefaultCount = 1)]
                Spread<Style> Test
        ) { }
    }

    [ProcessNode]
    public class TestXNode : TesterNode { }
}
