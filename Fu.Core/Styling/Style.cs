using Fu.Core.Base;
using VL.Lib.Collections;

namespace Fu.Core.Styling
{
    /// <summary>
    /// An immutable styling instruction: writes values into the style of a node, after its <see cref="Inputs"/> did.
    /// It is the single style type passed between style nodes and to <see cref="IStylable.SetStyle"/>.
    /// </summary>
    /// <remarks>
    /// Styles form a tree: every style runs its inputs depth first, left to right, then itself, so later instructions win.
    /// A style holds no node state, so one style can be set on many nodes, and one subtree can be shared by many trees.
    /// Derive from <see cref="Style{TNode}"/> for values owned by a node type, or from <see cref="YogaStyle"/> for Yoga style.
    /// </remarks>
    /// <param name="Inputs">
    /// The styles that run before this one. Null items are skipped.
    /// A list that is not a <see cref="Spread{T}"/> is copied, so the style stays immutable.
    /// </param>
    public abstract record Style(IReadOnlyList<Style> Inputs)
    {
        private readonly Spread<Style> _inputs = Inputs?.ToSpread() ?? Spread<Style>.Empty;

        /// <summary>
        /// The styles that run before this one, in order.
        /// </summary>
        public IReadOnlyList<Style> Inputs => _inputs;

        /// <summary>
        /// Writes this instruction, without its inputs, into the staged style of <paramref name="node"/>.
        /// Called by the styling pass of the node; must only make deterministic assignments.
        /// Throw when the node can't take the instruction; the pass reports it and goes on with the next one.
        /// </summary>
        public abstract void Build(NodeBase node);

        /// <summary>
        /// Compares the type and the <see cref="Inputs"/> item by item, since <see cref="Spread{T}"/> compares by reference.
        /// Derived records add their own values, so equal trees compare equal.
        /// </summary>
        public virtual bool Equals(Style? other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other is null || EqualityContract != other.EqualityContract)
                return false;

            var inputs = _inputs;
            var otherInputs = other._inputs;
            if (ReferenceEquals(inputs, otherInputs))
                return true;

            if (inputs.Count != otherInputs.Count)
                return false;

            for (var i = 0; i < inputs.Count; i++)
            {
                if (!object.Equals(inputs[i], otherInputs[i]))
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(EqualityContract);
            foreach (var input in _inputs)
                hash.Add(input);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// A style for values owned by <typeparamref name="TNode"/>, written through the staging API of that node type.
    /// Set on a node of another type, it is reported on that node and skipped.
    /// </summary>
    public abstract record Style<TNode>(IReadOnlyList<Style> Inputs) : Style(Inputs)
        where TNode : NodeBase
    {
        /// <summary>
        /// Writes this instruction into the staged values of <paramref name="node"/>.
        /// Must only make deterministic assignments, without reading live state or keeping the node.
        /// </summary>
        public abstract void Build(TNode node);

        public sealed override void Build(NodeBase node) =>
            Build(
                node as TNode
                    ?? throw new ArgumentException($"{GetType().Name} requires a {typeof(TNode).Name} node.")
            );
    }

    /// <summary>
    /// Joins its inputs without an instruction of its own. Built by style nodes with several inputs and nothing set.
    /// </summary>
    internal sealed record CombinedStyle(IReadOnlyList<Style> Inputs) : Style(Inputs)
    {
        public override void Build(NodeBase node) { }
    }
}
