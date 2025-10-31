using VL.Fu.Core;
using VL.Fu.Core.HitTest;

namespace VL.Fu.Extensions
{
    public static class FuNodeExtensions
    {
        public static IFuNode? PerformHitTest(IFuNode rootNode, FuCursor cursor)
        {
            // Iterate through children in reverse order to check the top-most visual elements first
            for (int i = rootNode.Children.Count - 1; i >= 0; i--)
            {
                // Must cast or ensure children are the correct type for your implementation
                if (rootNode.Children[i] is IFuNode childNode)
                {
                    // Recursively search the child's subtree (Depth-First)
                    var hitInChildTree = PerformHitTest(childNode, cursor);
                    if (hitInChildTree != null)
                    {
                        return hitInChildTree; // Return the hit immediately and stop searching the rest of the tree
                    }
                }
            }

            // If no child captured the hit, check this current node
            if (rootNode is IHitTestProvider hitProvider)
            {
                if (hitProvider.HitTest(cursor))
                {
                    return rootNode; // Return this node immediately and stop
                }
            }

            // If nothing here or below hit, return null
            return null;
        }
    }
}
