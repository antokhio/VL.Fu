using Stride.Core.Mathematics;

namespace VL.Fu.Core.HitTest
{
    /// <summary>
    /// Defines the contract for an object that can perform area-testing on a FuNode.
    /// </summary>
    public interface IAreaTest
    {
        /// <summary>
        /// Determines whether the given node is fully contained within the specified area.
        /// </summary>
        /// <param name="node">The node to test.</param>
        /// <param name="area">The rectangular area to test against.</param>
        /// <returns>True if the node is fully contained within the area; otherwise, false.</returns>
        bool IsContainedIn(IFuNode node, RectangleF area);
    }
}
