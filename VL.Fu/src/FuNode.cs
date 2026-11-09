using VL.Core;
using VL.Core.Import;
using VL.Core.PublicAPI;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Region;
using VL.Fu.Core.Rendering;
using VL.Fu.Core.Tree;
using VL.Lib.Collections;
using VL.Model;

namespace VL.Fu
{
    /// <summary>
    /// A node of a Fu tree, with every feature of the element chain: tree, style, layout, interaction, rendering and region,
    /// and its parent and children typed as <see cref="IFuNode"/> again.
    /// </summary>
    /// <remarks>
    /// Every node in a tree derives from <see cref="RegionProvider"/>, which closes the chain at this interface.
    /// Other implementations are ignored with a warning when passed as children, since the tree relies on internals they lack.
    /// </remarks>
    public interface IFuNode : IRegionProvider, ITraversable<IFuNode> { }

    [ProcessNode(Name = "FuNode", HasStateOutput = true)]
    [Region(SupportedBorderControlPoints = ControlPointType.None)]
    public class FuNode : RegionProvider, IFuNode
    {
        public FuNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        [Fragment(Order = PinOrder.Children)]
        public virtual void SetChildren(
            [Pin(PinGroupKind = PinGroupKind.Collection, PinGroupDefaultCount = 1)]
                Spread<IFuNode> children
        )
        {
            base.SetChildren(children);
        }
    }

    [ProcessNode(Name = "FuNode (Spectral)", HasStateOutput = true)]
    public class FuNodeSpectral : FuNode
    {
        [Fragment(Order = PinOrder.Children)]
        public FuNodeSpectral([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public override void SetChildren(Spread<IFuNode> children)
        {
            base.SetChildren(children);
        }
    }
}
