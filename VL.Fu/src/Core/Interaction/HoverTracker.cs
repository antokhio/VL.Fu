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
        private int _pointerId = PointerIds.Mouse;

        /// <summary>
        /// The nodes the pointer is over, from the root down.
        /// </summary>
        public IReadOnlyList<IInteractive> Path => _path;

        /// <summary>
        /// The pointer that moved the hover path last: −1 for the mouse, otherwise the primary touch.
        /// </summary>
        public int PointerId => _pointerId;

        /// <summary>
        /// Replaces the hover path with <paramref name="next"/>, copying the old one into <paramref name="previous"/>.
        /// Returns how many nodes both share from the root down: the old nodes after them were left, deepest first,
        /// and the new ones after them were entered, outermost first.
        /// </summary>
        public int Update(IReadOnlyList<IInteractive> next, int pointerId, List<IInteractive> previous)
        {
            previous.Clear();
            previous.AddRange(_path);

            var common = 0;
            while (common < _path.Count && common < next.Count && ReferenceEquals(_path[common], next[common]))
                common++;

            _path.Clear();
            for (var i = 0; i < next.Count; i++)
                _path.Add(next[i]);

            _pointerId = pointerId;
            return common;
        }

        /// <summary>
        /// Keeps only the first <paramref name="count"/> nodes, after the others left the tree.
        /// </summary>
        public void Truncate(int count)
        {
            if (count < _path.Count)
                _path.RemoveRange(count, _path.Count - count);
        }
    }
}
