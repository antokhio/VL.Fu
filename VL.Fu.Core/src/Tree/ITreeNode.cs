using VL.Lib.Collections;

namespace VL.Fu.Core
{
    public interface ITreeNode
    {
        Spread<ITreeNode> Children { get; }
        ITreeNode? Parent { get; set; }
        IEnumerator<ITreeNode> GetEnumerator();
        void SetChildren(IEnumerable<ITreeNode> newChildren);
    }
}
