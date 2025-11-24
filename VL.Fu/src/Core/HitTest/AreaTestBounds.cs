namespace VL.Fu.Core.HitTest
{
    /// <summary>
    /// Default strategy: Area test checks if the node's bounding box is inside the selection shape.
    /// </summary>
    public class AreaTestBounds : IAreaTest
    {
        public bool IsContainedIn(IFuNode node, ISelectionShape shape)
        {
            if (node.Bounds is null)
                return false;

            return shape.Contains(node.Bounds.Value);
        }
    }
}
