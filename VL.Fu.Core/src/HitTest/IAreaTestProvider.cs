using VL.Fu.Core.HitTest;

namespace VL.Fu.Core
{
    /// <summary>
    /// Interface for objects that can report if they are inside a selection.
    /// </summary>
    public interface IAreaTestProvider
    {
        /// <summary>
        /// Executes the area test using the configured strategy.
        /// </summary>
        /// <param name="shape">Selection shape</param>
        bool IsContainedIn(ISelectionShape shape);
    }
}
