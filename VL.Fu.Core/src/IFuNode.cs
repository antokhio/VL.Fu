using VL.Fu.Core;
using VL.Fu.Core.AreaTest;
using VL.Fu.Core.HitTest;
using VL.Lib.Collections;
using VL.Skia;

namespace VL.Fu
{
    public interface IFuNode : ITreeNode, ILayer, IHitTestProvider, IAreaTestProvider
    {
        FuNodeState State { get; set; }

        Spread<IFuBehaviour> Behaviours { get; }
    }
}
