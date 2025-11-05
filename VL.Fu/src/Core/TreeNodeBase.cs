using System.Collections;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Lib.Collections;

namespace VL.Fu.Core
{
    /// <summary>
    /// An abstract base class that combines tree hierarchy with service consumer capabilities.
    /// </summary>
    /// <remarks>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class TreeNodeBase : RepositoryConsumer, ITreeNode
    {
        public ITreeNode? Parent { get; set; }
        public IEnumerable<ITreeNode> Children { get; private set; } = Spread<ITreeNode>.Empty;

        /// <summary>
        /// Explicit implementation for ITreeNode. Users should use the SetInput fragment.
        /// </summary>
        void ITreeNode.SetChildren(IEnumerable<ITreeNode> newChildren)
        {
            foreach (var oldChild in Children)
            {
                if (oldChild != null)
                    oldChild.Parent = null;
            }

            Children = newChildren;

            foreach (var newChild in Children)
            {
                newChild.Parent = this;
            }
        }

        /// <summary>
        /// Sets the input children for this node. This is the main input pin for creating hierarchies.
        /// </summary>
        [Fragment(Order = PinOrder.Input)]
        public virtual void SetInput(
            [Pin(PinGroupKind = Model.PinGroupKind.Collection, PinGroupDefaultCount = 1)]
                Spread<IFuNode> input
        )
        {
            (this as ITreeNode).SetChildren(input);
        }

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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
