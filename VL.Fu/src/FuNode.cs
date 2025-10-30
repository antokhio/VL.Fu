using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Helpers;
using VL.Lib.Collections;

namespace VL.Fu
{
    [ProcessNode(Name = "FuNode")]
    public class FuNode : TreeNode<FuNode>, ITreeNode
    {
        public new Spread<FuNode> Children => _children.Value;
        protected readonly CachedProperty<Spread<FuNode>> _children = new(Spread<FuNode>.Empty);

        public virtual void SetChildren(
            [Pin(PinGroupKind = Model.PinGroupKind.Collection, PinGroupDefaultCount = 1)]
                Spread<FuNode> children
        ) => _children.TrySetValue(children, (oc, nc) => base.SetChildren(nc));
    }

    [ProcessNode(Name = "FuNode (Spectral)")]
    public class FuSpectralNode : FuNode
    {
        public override void SetChildren(Spread<FuNode> children)
        {
            base.SetChildren(children);
        }
    }
}
