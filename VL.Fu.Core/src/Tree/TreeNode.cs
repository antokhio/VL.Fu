using System.Collections;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Lib.Collections;
using VL.Model;

namespace VL.Fu.Core
{
    /// <summary>
    /// Abstract base class that combines tree hierarchy logic with service consumption capabilities.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class TreeNode : ContextConsumer, ITreeNode
    {
        public ITreeNode? Parent { get; set; }

        // Backing property for children. Initialized to Empty to avoid nulls.
        public IEnumerable<ITreeNode> Children { get; private set; } = Spread<ITreeNode>.Empty;

        // Caching reference to the input Spread to prevent redundant graph updates.
        private object? _cachedChildrenInput;

        protected TreeNode(NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// Sets the children for this node.
        /// This is the primary input pin for the VL patch.
        /// </summary>
        [Fragment(Order = PinOrder.Input)]
        public virtual void SetChildren(
            [Pin(PinGroupKind = PinGroupKind.Collection, PinGroupDefaultCount = 1)]
                Spread<IFuNode> children
        )
        {
            // Fast path: if the Spread object hasn't changed, do nothing.
            if (ReferenceEquals(children, _cachedChildrenInput))
                return;

            _cachedChildrenInput = children;

            SetChildrenInternal(children);
        }

        /// <summary>
        /// Explicit implementation for ITreeNode.
        /// This allows the node to be manipulated as a generic ITreeNode by the system.
        /// </summary>
        void ITreeNode.SetChildren(IEnumerable<ITreeNode> newChildren)
        {
            // We invalidate the cache because the structure is being modified
            // outside the standard Spread input mechanism.
            _cachedChildrenInput = null;

            SetChildrenInternal(newChildren);
        }

        // Shared logic to rewire parents. Private to keep the API clean.
        private void SetChildrenInternal(IEnumerable<ITreeNode>? newChildren)
        {
            // 1. Detach old children
            // We only nullify the parent if it still points to us.
            // This prevents messing up the graph if a child was already moved elsewhere.
            foreach (var oldChild in Children)
            {
                if (oldChild != null && ReferenceEquals(oldChild.Parent, this))
                {
                    oldChild.Parent = null;
                }
            }

            // 2. Update collection
            // Cast to ITreeNode is required as Children is IEnumerable<ITreeNode>
            Children = newChildren?.Cast<ITreeNode>() ?? Spread<ITreeNode>.Empty;

            // 3. Attach new children
            foreach (var newChild in Children)
            {
                if (newChild != null)
                {
                    newChild.Parent = this;
                }
            }
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
