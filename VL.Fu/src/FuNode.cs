using VL.Core;
using VL.Core.Import;
using VL.Core.PublicAPI;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Region;
using VL.Fu.Core.Tree;
using VL.Lib.Collections;
using VL.Model;
using VL.Skia;

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

        /// <summary>
        /// Attaches behaviors to this node, in pin order. Fu's behaviors and behaviors patched in VL are treated alike.
        /// Empty pins and Fu nodes are ignored, and a behavior connected twice is attached once.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetBehaviors(Spread<IBehavior> behaviors)
        {
            base.SetBehaviors(behaviors);
        }

        /// <summary>
        /// Runs the patch inside the node, then lays the tree out if this node is its root.
        /// Children are upstream, so they ran before this and the layout sees this frame's tree and styles.
        /// The root lays out into its bounds if set, otherwise into the viewport of its last render.
        /// </summary>
        [Fragment]
        public override void Update()
        {
            base.Update();
            CalculateRootLayout();
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
