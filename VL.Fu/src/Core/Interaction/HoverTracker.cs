namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Keeps the hover path of the mouse or the primary touch and tells which nodes a new path left and entered (v2 §3.5).
    /// </summary>
    /// <remarks>
    /// It only computes; the dispatcher turns the result into enter and leave notifications.
    /// Hover isn't press state, so cancels don't touch it.
    /// </remarks>
    internal sealed class HoverTracker
    {
        private readonly List<IInteractive> _path = new();

        /// <summary>
        /// The nodes the pointer is over, from the root down.
        /// </summary>
        public IReadOnlyList<IInteractive> Path => _path;

        /// <summary>
        /// Replaces the hover path with <paramref name="next"/> and fills <paramref name="left"/> with the nodes that aren't on it
        /// anymore, deepest first, and <paramref name="entered"/> with the new ones, outermost first. Both are cleared first.
        /// </summary>
        public void Update(IReadOnlyList<IInteractive> next, List<IInteractive> left, List<IInteractive> entered)
        {
            left.Clear();
            entered.Clear();

            var common = 0;
            while (common < _path.Count && common < next.Count && ReferenceEquals(_path[common], next[common]))
                common++;

            for (var i = _path.Count - 1; i >= common; i--)
                left.Add(_path[i]);

            for (var i = common; i < next.Count; i++)
                entered.Add(next[i]);

            _path.Clear();
            for (var i = 0; i < next.Count; i++)
                _path.Add(next[i]);
        }
    }
}
