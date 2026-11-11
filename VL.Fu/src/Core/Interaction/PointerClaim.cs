namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// What a behavior wants a pressed pointer for. When several ask for the same pointer, the higher kind wins.
    /// </summary>
    public enum GestureKind
    {
        Click = 1,
        Drag = 2,
        Pinch = 3,
    }

    /// <summary>
    /// A node's hold on one or more pressed pointers (v2 §5.1). While a pointer is claimed, its input goes to the claim node only;
    /// the node's ancestors forward it without receiving it.
    /// </summary>
    /// <remarks>
    /// Behaviors of the same kind on the same node share one claim as its members. A claim ends when its last pointer lifts,
    /// when its last member releases it, when a claim of a higher kind takes one of its pointers, on a device loss,
    /// or when the claim node leaves the tree. Members compare by reference.
    /// </remarks>
    public sealed class PointerClaim
    {
        private readonly IInteractive _node;
        private readonly GestureKind _kind;
        private readonly List<int> _pointers;
        private readonly List<object> _members = new();
        private bool _ended;

        internal PointerClaim(IInteractive node, GestureKind kind, IReadOnlyList<int> pointers, object firstMember)
        {
            _node = node;
            _kind = kind;
            _pointers = new List<int>(pointers);
            _members.Add(firstMember);
        }

        /// <summary>
        /// The node that holds the pointers. Their input ends there.
        /// </summary>
        public IInteractive Node => _node;

        public GestureKind Kind => _kind;

        /// <summary>
        /// The pointers the claim holds: −1 for the mouse, otherwise touch ids. A pinch holds two.
        /// </summary>
        public IReadOnlyList<int> Pointers => _pointers;

        /// <summary>
        /// The owners that requested the claim and were granted it: behaviors on <see cref="Node"/>, or content in its layer.
        /// </summary>
        public IReadOnlyList<object> Members => _members;

        /// <summary>
        /// False once the claim ended. Its members keep the facts they were granted, but its pointers aren't held anymore.
        /// </summary>
        public bool IsActive => !_ended;

        public bool HasMember(object owner)
        {
            for (var i = 0; i < _members.Count; i++)
            {
                if (ReferenceEquals(_members[i], owner))
                    return true;
            }

            return false;
        }

        internal bool Holds(int pointerId) => _pointers.Contains(pointerId);

        internal void AddMember(object owner)
        {
            if (!HasMember(owner))
                _members.Add(owner);
        }

        /// <summary>
        /// Removes <paramref name="owner"/> from the members. Returns whether it was one.
        /// </summary>
        internal bool RemoveMember(object owner)
        {
            for (var i = 0; i < _members.Count; i++)
            {
                if (ReferenceEquals(_members[i], owner))
                {
                    _members.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        internal void RemovePointer(int pointerId) => _pointers.Remove(pointerId);

        internal void End() => _ended = true;

        public override string ToString() => $"{_kind} claim on {_node} ({string.Join(", ", _pointers)})";
    }
}
