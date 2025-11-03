using Stride.Core.Mathematics;

namespace VL.Fu.Core.Selections
{
    /// <summary>
    /// Defines the contract for a selection strategy, like box selection or single-click selection.
    /// </summary>
    public interface ISelectionMode
    {
        /// <summary>
        /// Gets the rectangle representing the current selection area.
        /// </summary>
        RectangleF SelectionRectangle { get; }

        /// <summary>
        /// Gets a value indicating whether the selection gesture is currently active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Called by the host behaviour when a selection gesture begins.
        /// </summary>
        void Start(Vector2 startPosition, IReadOnlyList<FuKey> modifiers);

        /// <summary>
        /// Called by the host behaviour as the selection gesture updates.
        /// </summary>
        void UpdateSelection(Vector2 currentPosition, IReadOnlyList<FuKey> modifiers);

        /// <summary>
        /// Called by the host behaviour when the selection gesture ends.
        /// </summary>
        void End();

        /// <summary>
        /// Resets the mode to its initial state.
        /// </summary>
        void Reset();
    }
}
