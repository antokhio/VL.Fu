using VL.Fu.Core;
using VL.Fu.Extensions;

namespace VL.Fu.Services
{
    public class InteractionService
    {
        public void Update(
            IFuNode root,
            IEnumerable<FuCursor> cursors,
            IEnumerable<FuKey> keys,
            IEnumerable<FuKey> modifiers
        )
        {
            foreach (var node in root.TraverseDepthFirstPreOrder<IFuNode>())
            {
                if (node is IFuNode fuNode)
                {
                    // Sort behaviors by priority (highest priority runs first)
                    var sortedBehaviors = fuNode
                        .Behaviours.Where(x => x is not null)
                        .OrderByDescending(b => b.Priority);

                    if (sortedBehaviors.Any())
                    {
                        var imidiateState = FuNodeState.Identity();

                        foreach (var behavior in sortedBehaviors)
                        {
                            if (
                                behavior.IsEnabled
                                && behavior.TryActivate(fuNode, cursors, keys, modifiers)
                            )
                            {
                                // Evaluate the behavior's proposed state
                                var evaluatedState = behavior.Evaluate(
                                    fuNode,
                                    cursors,
                                    keys,
                                    modifiers
                                );

                                imidiateState = imidiateState.OptimisticMerge(evaluatedState);
                            }
                        }

                        fuNode.State = imidiateState;
                    }
                }
            }
        }
    }
}
