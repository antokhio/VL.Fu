using VL.Fu.Core.Input;

namespace VL.Fu.Core.HitTest
{
    /// <summary>
    /// Provides an interface for testing if a pointer is currently over an object.
    /// </summary>
    public interface IHitTestProvider
    {
        /// <summary>
        /// Determines whether the specified pointer is currently over the object.
        /// </summary>
        /// <param name="pointer">The pointer to test against.</param>
        /// <returns>True if the pointer is over the object; otherwise, false.</returns>
        bool HitTest(FuPointer pointer);
    }
}
