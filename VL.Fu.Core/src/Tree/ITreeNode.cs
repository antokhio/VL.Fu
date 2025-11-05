namespace VL.Fu.Core
{
    public interface ITreeNode : IEnumerable<ITreeNode>
    {
        IEnumerable<ITreeNode> Children { get; }
        ITreeNode? Parent { get; set; }
        IEnumerator<ITreeNode> GetEnumerator();
        void SetChildren(IEnumerable<ITreeNode> newChildren);
    }
}
