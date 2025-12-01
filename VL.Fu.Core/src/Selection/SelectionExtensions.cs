using VL.Fu.Extensions;

namespace VL.Fu.Core.Selection
{
    public static class SelectionExtensions
    {
        /// <summary>
        /// Traverses up the visual tree to find the nearest Selection Provider.
        /// This establishes the "Selection Scope".
        /// </summary>
        public static ISelectionProvider? FindSelectionProvider(this IFuNode node)
        {
            foreach (var ancestor in node.Ancestors())
            {
                if (ancestor is IInteractable interactable)
                {
                    var provider = interactable
                        .Behaviours.OfType<ISelectionProvider>()
                        .FirstOrDefault();
                    if (provider != null)
                        return provider;
                }
            }
            return null;
        }
    }
}
