using System.Collections;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Repository;
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

                // If the new child is a repository consumer, propagate our own context ID to it.
                // This ensures the context flows down the entire tree.
                if (newChild is IRepositoryConsumer childConsumer)
                {
                    childConsumer.SetContextId(this.ContextId);
                }
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

        public override void SetContextId(int contextId)
        {
            // If the context ID hasn't changed, do nothing.
            if (contextId == this.ContextId)
                return;

            // First, set our own context ID.
            base.SetContextId(contextId);

            // Now, push the new context ID down to all our existing children.
            // This handles cases where the tree is moved or the root context is established after the tree is built.
            foreach (var child in Children)
            {
                if (child is IRepositoryConsumer childConsumer)
                {
                    childConsumer.SetContextId(contextId);
                }
            }
        }
    }
}
