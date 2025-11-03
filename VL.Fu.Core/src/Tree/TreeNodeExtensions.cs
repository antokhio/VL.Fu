using VL.Fu.Core;

namespace VL.Fu.Extensions
{
    public static class TreeNodeExtensions
    {
        /// <summary>
        /// Performs a breadth-first traversal of the tree starting from the specified root node.
        /// </summary>
        /// <param name="root">The root node to start the traversal from.</param>
        /// <returns>An enumerable collection of tree nodes in breadth-first order.</returns>
        public static IEnumerable<ITreeNode> TraverseBreadthFirst(this ITreeNode root)
        {
            var queue = new Queue<ITreeNode>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                yield return current;

                foreach (var child in current.Children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        /// <summary>
        /// Performs a post-order traversal of the tree starting from the specified root node.
        /// </summary>
        /// <param name="root">The root node to start the traversal from.</param>
        /// <returns>An enumerable collection of tree nodes in post-order (children before parent).</returns>
        public static IEnumerable<ITreeNode> TraversePostOrder(this ITreeNode root)
        {
            if (root is null)
                yield break;

            foreach (var child in root.Children)
            {
                // Recursive call for each child
                foreach (var descendant in child.TraversePostOrder())
                {
                    yield return descendant;
                }
            }
            // Return the root node after all its descendants
            yield return root;
        }

        /// <summary>
        /// Returns an enumerable collection of all ancestor nodes (parent, grandparent, etc.) of the current node,
        /// starting from the immediate parent and moving toward the root.
        /// </summary>
        /// <param name="node">The node to start the search from.</param>
        /// <returns>An enumerable collection of ancestor nodes.</returns>
        public static IEnumerable<ITreeNode> Ancestors(this ITreeNode node)
        {
            var current = node.Parent;
            while (current != null)
            {
                yield return current;
                current = current.Parent;
            }
        }

        /// <summary>
        /// Performs a pre-order, depth-first traversal of the tree starting from the specified root node.
        /// </summary>
        /// <param name="root">The root node to start the traversal from.</param>
        /// <returns>An enumerable collection of tree nodes in pre-order, depth-first order.</returns>
        public static IEnumerable<ITreeNode> TraverseDepthFirstPreOrder<T>(this ITreeNode root)
            where T : ITreeNode
        {
            // Equivalent to the enumerator implemented in TreeNode<T>
            yield return (T)root;
            foreach (var child in root.Children)
            {
                foreach (var descendant in child.TraverseDepthFirstPreOrder<T>()) // Recursive call
                {
                    yield return (T)descendant;
                }
            }
        }
    }
}
