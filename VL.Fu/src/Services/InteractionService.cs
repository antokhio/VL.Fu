using VL.Fu.Core;
using VL.Fu.Extensions;
using VL.Lib.Collections;

namespace VL.Fu.Services
{
    public class InteractionService
    {
        private readonly Dictionary<IFuNode, IFuBehaviour> _activeNodeBehaviours = new();
        private readonly HashSet<int> _capturedCursorIds = new();

        /// <summary>
        /// Updates the interaction state by traversing the node tree and processing behaviours.
        /// </summary>
        public void Update(
            IFuNode root,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        )
        {
            if (root is null)
                return;

            _capturedCursorIds.Clear();

            // --- 1. Advance and Deactivate Phase ---
            var nodesToDeactivate = new List<IFuNode>();
            foreach (var (node, activeBehaviour) in _activeNodeBehaviours)
            {
                if (activeBehaviour.TryAdvance(node, cursors, keys, modifiers))
                {
                    foreach (var cursor in cursors.Where(c => node.HitTest(c)))
                    {
                        _capturedCursorIds.Add(cursor.Id);
                    }
                }
                else
                {
                    nodesToDeactivate.Add(node);
                }
            }

            foreach (var node in nodesToDeactivate)
            {
                _activeNodeBehaviours.Remove(node);
            }

            // --- 2. Activation Phase ---
            // Traverse post-order AND filter for nodes that are IFuNode.
            // No casting needed!
            foreach (var node in root.TraversePostOrder().OfType<IFuNode>())
            {
                if (_activeNodeBehaviours.ContainsKey(node))
                {
                    continue;
                }

                var availableCursors = cursors
                    .Where(c => !_capturedCursorIds.Contains(c.Id) && node.HitTest(c))
                    .ToList();

                if (availableCursors.Count == 0)
                {
                    continue;
                }

                var activatingBehaviour = node
                    .Behaviours.Where(b => b.IsEnabled)
                    .OrderByDescending(b => b.Priority)
                    .FirstOrDefault(b => b.TryActivate(node, availableCursors, keys, modifiers));

                if (activatingBehaviour != null)
                {
                    _activeNodeBehaviours[node] = activatingBehaviour;

                    foreach (var cursor in availableCursors)
                    {
                        _capturedCursorIds.Add(cursor.Id);
                    }
                }
            }
        }
    }
}
