using System.Collections;
using VL.Lib.Collections;

namespace VL.Fu.Core
{
    public class TreeNode<T> : IEnumerable<ITreeNode>, ITreeNode
        where T : ITreeNode
    {
        public ITreeNode? Parent { get; set; }
        public Spread<ITreeNode> Children { get; private set; } = Spread<ITreeNode>.Empty;

        public TreeNode() { }

        /// <summary>
        /// Sets a new collection of children for the current node, replacing any existing children.
        /// Null entries in the input collection are ignored.
        /// </summary>
        public void SetChildren(IEnumerable<ITreeNode> newChildren)
        {
            foreach (var oldChild in Children)
            {
                oldChild.Parent = null;
            }

            Children = newChildren.Where(x => x is not null).ToSpread();

            foreach (var newChild in Children)
            {
                newChild.Parent = this;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the entire tree in a pre-order, depth-first fashion,
        /// starting with the current node.
        public IEnumerator<ITreeNode> GetEnumerator()
        {
            yield return this;
            foreach (var child in Children)
            {
                foreach (var descendant in child)
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the entire tree.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
