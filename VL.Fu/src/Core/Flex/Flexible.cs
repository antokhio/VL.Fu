using VL.Core;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Core.Flex
{
    public interface IFlexible
    {
        unsafe YGNode* Handle { get; }
    }

    public abstract class Flexible : TreeNode, IFlexible
    {
        protected unsafe YGNode* _nodeHandle = YGNode.New();
        public unsafe YGNode* Handle => _nodeHandle;

        protected Flexible(NodeContext nodeContext)
            : base(nodeContext) { }

        public override void SetChildren(
            [Pin(PinGroupKind = PinGroupKind.Collection, PinGroupDefaultCount = 1)]
                Spread<IFuNode> children
        )
        {
            base.SetChildren(children);
        }
    }
}
