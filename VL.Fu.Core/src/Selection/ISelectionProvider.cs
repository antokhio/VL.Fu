namespace VL.Fu.Core.Selection
{
    public interface ISelectionProvider
    {
        /// <summary>
        /// Checks if a specific node is currently in the selection set.
        /// </summary>
        bool IsSelected(IFuNode node);

        /// <summary>
        /// Modifies the selection state.
        /// </summary>
        void Select(IFuNode node, bool add, bool remove);

        /// <summary>
        /// Executes a generic action on all selected nodes.
        /// </summary>
        /// <param name="action">The action to perform on each selected node.</param>
        /// <param name="exclude">An optional node to skip (e.g. the initiator of an interaction).</param>
        void DispatchToSelected(Action<IFuNode> action, IFuNode? exclude = null);

        /// <summary>
        /// Expose the read-only set for the InteractionService
        /// </summary>
        IReadOnlySet<IFuNode> SelectedNodes { get; }
    }
}
