using VL.Fu.Core.Input;

namespace VL.Fu.Core.HitTest
{
    /// <summary>
    /// Defines the contract for an object that can perform hit-testing on a FuNode.
    /// </summary>
    public interface IHitTest
    {
        /// <summary>
        /// Performs a hit test on the given node using the specified pointer.
        /// </summary>
        /// <param name="node">The node to test against.</param>
        /// <param name="pointer">The pointer to test.</param>
        /// <returns>True if the pointer hits the node; otherwise, false.</returns>
        bool HitTest(IFuNode node, FuPointer pointer);
    }
}
