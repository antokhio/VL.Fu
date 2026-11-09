using Microsoft.Extensions.Logging;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Lib.Collections;
using VL.Model;

namespace VL.Fu.Core.Tree
{
    /// <summary>
    /// Untyped, read-only view of a tree node. Lets code that doesn't know the concrete
    /// node type (VL patches, renderers, hit-testers) walk any Fu tree.
    /// </summary>
    [Name("Traversable (Ungeneric)")]
    public interface ITraversable
    {
        ITraversable? Parent { get; }
        IReadOnlyList<ITraversable> Children { get; }
    }

    /// <summary>
    /// Strongly typed tree node: parent and children are <typeparamref name="T"/>.
    /// Deliberately not enumerable, so VL doesn't offer every sequence operation on a node; enumerate <see cref="Children"/> instead.
    /// </summary>
    public interface ITraversable<T> : ITraversable
        where T : class, ITraversable<T>
    {
        new T? Parent { get; }
        new IReadOnlyList<T> Children { get; }

        /// <summary>
        /// Moves this node under <paramref name="parent"/>, appending it as the last child.
        /// Passing null detaches the node from its current parent.
        /// </summary>
        void SetParent(T? parent);

        /// <summary>
        /// Replaces the children of this node. Parent links are kept consistent:
        /// removed children get detached, and children that belonged to another node are taken from it.
        /// </summary>
        void SetChildren(IReadOnlyList<T>? children);
    }

    /// <summary>
    /// Base tree node using the self-referencing generic pattern: <c>class Foo : Traversable&lt;Foo&gt;</c>,
    /// or <c>class Foo : Traversable&lt;IFoo&gt;, IFoo</c> to type the tree by an interface the node implements.
    /// </summary>
    /// <remarks>
    /// With an interface, only nodes deriving from this class can join the tree (see <see cref="AcceptsChild"/>):
    /// the tree keeps parent links consistent through the internals of every node, which a foreign implementation lacks.
    /// </remarks>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Traversable<TSelf> : NodeBase, ITraversable<TSelf>
        where TSelf : class, ITraversable<TSelf>
    {
        private TSelf? _parent;
        private Spread<TSelf> _children = Spread<TSelf>.Empty;

        protected Traversable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext)
        {
            if (this is not TSelf)
                throw new InvalidOperationException(
                    $"{GetType().Name} must derive from {typeof(TSelf).Name} to be a Traversable<{typeof(TSelf).Name}>."
                );
        }

        private TSelf Self => (TSelf)(object)this;

        public TSelf? Parent => _parent;

        public IReadOnlyList<TSelf> Children => _children;

        ITraversable? ITraversable.Parent => _parent;

        IReadOnlyList<ITraversable> ITraversable.Children => _children;

        public void SetParent(TSelf? parent)
        {
            if (ReferenceEquals(parent, _parent))
                return;

            if (parent is null)
            {
                var oldParent = AsNode(_parent!);
                oldParent.SetChildrenCore(Without(oldParent._children, Self));
            }
            else if (parent is not Traversable<TSelf> parentNode)
            {
                Log(
                    LogLevel.Warning,
                    $"Can't move '{this}' under '{parent}': it doesn't derive from {nameof(Traversable<TSelf>)}<{typeof(TSelf).Name}>."
                );
            }
            else if (ReferenceEquals(parent, this) || Self.IsAncestorOf(parent))
            {
                Log(
                    LogLevel.Warning,
                    $"Can't move '{this}' under '{parent}': a node can't be a child of itself or of its own descendants."
                );
            }
            else
            {
                var builder = parentNode._children.ToBuilder();
                builder.Add(Self);
                parentNode.SetChildrenCore(builder.ToSpread());
            }
        }

        /// <summary>
        /// Accepts any list. A <see cref="Spread{T}"/> (what VL passes in) is stored as is;
        /// any other list is copied once, so later changes to it don't affect the tree.
        /// An unchanged list is recognized before copying, so passing the same nodes every frame costs no allocation.
        /// </summary>
        public void SetChildren(IReadOnlyList<TSelf>? children)
        {
            if (SameNodes(_children, children ?? Spread<TSelf>.Empty))
                return;

            SetChildrenCore(ToSpread(children));
        }

        private void SetChildrenCore(Spread<TSelf> next)
        {
            var previous = _children;

            if (SameNodes(previous, next))
                return;

            next = Sanitize(next);

            if (SameNodes(previous, next))
                return;

            var nextSet = new HashSet<TSelf>(next, ReferenceEqualityComparer.Instance);

            foreach (var child in previous)
            {
                if (!nextSet.Contains(child))
                    AsNode(child).ChangeParent(null);
            }

            _children = next;

            foreach (var child in next)
            {
                var node = AsNode(child);
                var oldParent = node._parent;
                if (ReferenceEquals(oldParent, this))
                    continue;

                if (oldParent is not null)
                    AsNode(oldParent).DetachChild(child);

                node.ChangeParent(Self);
            }

            OnChildrenChanged(previous, next);
        }

        /// <summary>
        /// Tells whether <paramref name="child"/> can join this node's children; rejected ones are ignored with a warning.
        /// The base accepts nodes deriving from <see cref="Traversable{TSelf}"/>, so the tree can reach their internals.
        /// Override to require more of a derived layer, and always include the base result.
        /// </summary>
        protected virtual bool AcceptsChild(TSelf child) => child is Traversable<TSelf>;

        /// <summary>
        /// Invoked on the node whose <see cref="Parent"/> just changed.
        /// Override to react to attach / detach, e.g. to insert or remove a Yoga node.
        /// </summary>
        protected virtual void OnParentChanged(TSelf? oldParent, TSelf? newParent) { }

        /// <summary>
        /// Invoked on the node whose <see cref="Children"/> just changed.
        /// By the time this runs, the parent links of all affected children are already updated.
        /// This is the single place every change ends up in (including a child being taken by another parent),
        /// so mirror the children into other structures here rather than by overriding <see cref="SetChildren"/>.
        /// </summary>
        protected virtual void OnChildrenChanged(
            IReadOnlyList<TSelf> oldChildren,
            IReadOnlyList<TSelf> newChildren
        ) { }

        /// <summary>
        /// Leaves the parent and releases the children, so no other node keeps referring to a disposed one.
        /// The children themselves are not disposed: they belong to whoever created them.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SetParent(null);
                SetChildrenCore(Spread<TSelf>.Empty);
            }

            base.Dispose(disposing);
        }

        private static Spread<TSelf> ToSpread(IReadOnlyList<TSelf>? children) =>
            children switch
            {
                null => Spread<TSelf>.Empty,
                Spread<TSelf> spread => spread,
                _ => children.ToSpread(),
            };

        // Safe for every node in the tree, since AcceptsChild only lets nodes deriving from this class in.
        private static Traversable<TSelf> AsNode(TSelf node) => (Traversable<TSelf>)(object)node;

        private static bool SameNodes(IReadOnlyList<TSelf> a, IReadOnlyList<TSelf> b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a.Count != b.Count)
                return false;
            for (var i = 0; i < a.Count; i++)
            {
                if (!ReferenceEquals(a[i], b[i]))
                    return false;
            }
            return true;
        }

        private static Spread<TSelf> Without(Spread<TSelf> spread, TSelf node)
        {
            var builder = spread.ToBuilder();
            for (var i = 0; i < builder.Count; i++)
            {
                if (ReferenceEquals(builder[i], node))
                {
                    builder.RemoveAt(i);
                    break;
                }
            }
            return builder.ToSpread();
        }

        private void ChangeParent(TSelf? newParent)
        {
            var oldParent = _parent;
            _parent = newParent;
            OnParentChanged(oldParent, newParent);
        }

        private void DetachChild(TSelf child)
        {
            var previous = _children;
            _children = Without(previous, child);
            OnChildrenChanged(previous, _children);
        }

        /// <summary>
        /// Drops children that would corrupt the tree (null entries, nodes <see cref="AcceptsChild"/> rejects, duplicates,
        /// the node itself or its ancestors)
        /// and reports them through <see cref="NodeBase.Log"/>, so a bad connection in the patch
        /// degrades gracefully instead of throwing.
        /// </summary>
        private Spread<TSelf> Sanitize(Spread<TSelf> next)
        {
            var seen = new HashSet<TSelf>(ReferenceEqualityComparer.Instance);
            SpreadBuilder<TSelf>? kept = null;
            List<string>? problems = null;

            for (var i = 0; i < next.Count; i++)
            {
                var child = next[i];
                var problem =
                    child is null ? "Ignored an empty child."
                    : !AcceptsChild(child)
                        ? $"Ignored '{child}': only Fu nodes can be children, not other implementations of {typeof(TSelf).Name}."
                    : !seen.Add(child) ? $"Ignored '{child}': it is already a child of this node."
                    : ReferenceEquals(child, this) || child.IsAncestorOf(Self)
                        ? $"Ignored '{child}': a node can't be a child of itself or of its own descendants."
                    : null;

                if (problem is null)
                {
                    kept?.Add(child!);
                    continue;
                }

                if (kept is null)
                {
                    kept = Spread.CreateBuilder<TSelf>(next.Count);
                    for (var j = 0; j < i; j++)
                        kept.Add(next[j]);
                }

                problems ??= new List<string>();
                if (!problems.Contains(problem))
                    problems.Add(problem);
            }

            if (kept is null)
                return next;

            Log(LogLevel.Warning, string.Join(Environment.NewLine, problems!));
            return kept.ToSpread();
        }
    }

    public static class TraversableExtensions
    {
        public static T Root<T>(this T node)
            where T : class, ITraversable<T>
        {
            var current = node;
            while (current.Parent is { } parent)
                current = parent;
            return current;
        }

        /// <summary>
        /// Walks up from the parent to the root: parent, grandparent, and so on.
        /// </summary>
        public static IEnumerable<T> Ancestors<T>(this T node)
            where T : class, ITraversable<T>
        {
            for (var current = node.Parent; current is not null; current = current.Parent)
                yield return current;
        }

        /// <summary>
        /// All descendants in depth-first pre-order, which matches document and paint order.
        /// The node itself is not included.
        /// </summary>
        public static IEnumerable<T> Descendants<T>(this T node)
            where T : class, ITraversable<T>
        {
            var stack = new Stack<(IReadOnlyList<T> List, int Index)>();
            stack.Push((node.Children, 0));

            while (stack.Count > 0)
            {
                var (list, index) = stack.Pop();
                if (index >= list.Count)
                    continue;

                stack.Push((list, index + 1));
                var child = list[index];
                yield return child;
                stack.Push((child.Children, 0));
            }
        }

        public static bool IsAncestorOf<T>(this T node, T other)
            where T : class, ITraversable<T>
        {
            for (var current = other.Parent; current is not null; current = current.Parent)
            {
                if (ReferenceEquals(current, node))
                    return true;
            }
            return false;
        }
    }
}
