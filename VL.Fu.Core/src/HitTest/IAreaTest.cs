namespace VL.Fu.Core.HitTest
{
    /// <summary>
    /// Defines the strategy for testing if a node is inside a selection shape.
    /// </summary>
    public interface IAreaTest
    {
        bool IsContainedIn(IFuNode node, ISelectionShape shape);
    }
}
