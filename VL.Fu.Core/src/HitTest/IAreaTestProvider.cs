using VL.Fu.Core.HitTest;

namespace VL.Fu.Core
{
    /// <summary>
    /// Interface for objects that can report if they are inside a selection.
    /// </summary>
    public interface IAreaTestProvider
    {
        bool IsContainedIn(ISelectionShape shape);
    }
}
