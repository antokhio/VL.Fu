using VL.Fu.Core.Input;

namespace VL.Fu.Core.HitTest
{
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
