using System.Collections;
using Fu.Core.Base;
using Microsoft.Extensions.Logging;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Model;

namespace Fu.Core.Tree
{
    /// <summary>
    /// Untyped, read-only view of a tree node. Lets code that doesn't know the concrete
    /// node type (VL patches, renderers, hit-testers) walk any Fu tree.
    /// </summary>
    public interface ITraversable
    {
        ITraversable? Parent { get; }
        IReadOnlyList<ITraversable> Children { get; }
    }

    /// <summary>
    /// Strongly typed tree node: parent and children are <typeparamref name="T"/>.
    /// Enumerating the node yields its direct children.
    /// </summary>
    public interface ITraversable<T> : ITraversable, IEnumerable<T>
        where T : class, ITraversable<T>
    {
        new T? Parent { get; }
        new Spread<T> Children { get; }

        /// <summary>
        /// Moves this node under <paramref name="parent"/>, appending it as the last child.
        /// Passing null detaches the node from its current parent.
        /// </summary>
        void SetParent(T? parent);

        /// <summary>
        /// Replaces the children of this node. Parent links are kept consistent:
        /// removed children get detached, and children that belonged to another node are taken from it.
        /// </summary>
        void SetChildren(Spread<T> children);
    }

    /// <summary>
    /// Base tree node using the self-referencing generic pattern:
    /// <c>class Foo : Traversable&lt;Foo&gt;</c>.
    /// </summary>
    public abstract class Traversable<TSelf> : NodeBase, ITraversable<TSelf>
        where TSelf : Traversable<TSelf>
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

        private TSelf Self => (TSelf)this;

        public TSelf? Parent => _parent;

        public Spread<TSelf> Children => _children;

        ITraversable? ITraversable.Parent => _parent;

        IReadOnlyList<ITraversable> ITraversable.Children => _children;

        public void SetParent(TSelf? parent)
        {
            if (ReferenceEquals(parent, _parent))
                return;

            if (parent is null)
            {
                _parent!.SetChildren(Without(_parent._children, Self));
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
                var builder = parent._children.ToBuilder();
                builder.Add(Self);
                parent.SetChildren(builder.ToSpread());
            }
        }

        public void SetChildren(Spread<TSelf> children)
        {
            var next = children ?? Spread<TSelf>.Empty;
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
                    child.ChangeParent(null);
            }

            _children = next;

            foreach (var child in next)
            {
                var oldParent = child._parent;
                if (ReferenceEquals(oldParent, this))
                    continue;

                if (oldParent is not null)
                    oldParent.DetachChild(child);

                child.ChangeParent(Self);
            }

            OnChildrenChanged(previous, next);
        }

        /// <summary>
        /// Invoked on the node whose <see cref="Parent"/> just changed.
        /// Override to react to attach / detach, e.g. to insert or remove a Yoga node.
        /// </summary>
        protected virtual void OnParentChanged(TSelf? oldParent, TSelf? newParent) { }

        /// <summary>
        /// Invoked on the node whose <see cref="Children"/> just changed.
        /// By the time this runs, the parent links of all affected children are already updated.
        /// </summary>
        protected virtual void OnChildrenChanged(
            Spread<TSelf> oldChildren,
            Spread<TSelf> newChildren
        ) { }

        public IEnumerator<TSelf> GetEnumerator() =>
            ((IEnumerable<TSelf>)_children).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static bool SameNodes(Spread<TSelf> a, Spread<TSelf> b)
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
        /// Drops children that would corrupt the tree (null entries, duplicates, the node itself or its ancestors)
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
            var stack = new Stack<(Spread<T> List, int Index)>();
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
