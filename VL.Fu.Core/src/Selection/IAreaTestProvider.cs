using VL.Fu.Core.Selection;

namespace VL.Fu.Core.HitTest
{
    public interface IAreaTestProvider
    {
        /// <summary>
        /// Executes the area test using the configured strategy.
        /// </summary>
        /// <param name="shape">Selection shape</param>
        bool IsContainedIn(ISelectionShape shape);
    }
}
