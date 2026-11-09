using System.Reflection;
using VL.Fu.Core.Region;
using Microsoft.Extensions.Logging;
using VL.Lib.Collections;

namespace VL.Fu.Tests.Core.Rendering
{
    [TestClass]
    public sealed class FuNodeTreeTests
    {
        private sealed class Node : RegionProvider
        {
            public Node(string name)
                : base(null!)
            {
                Name = name;
            }

            public string Name { get; }

            public List<string> Warnings { get; } = new();

            public override void Log(LogLevel level, string message, TimeSpan? lifetime = null)
            {
                if (level >= LogLevel.Warning)
                    Warnings.Add(message);
                base.Log(level, message, lifetime);
            }

            public override string ToString() => Name;
        }

        /// <summary>
        /// An <see cref="IFuNode"/> that doesn't derive from <see cref="RegionProvider"/>, like one implemented in a VL patch.
        /// Every member returns its default; the tree must not call any of them.
        /// </summary>
        public class Foreign : DispatchProxy
        {
            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
                targetMethod is { ReturnType: { IsValueType: true } type } && type != typeof(void)
                    ? Activator.CreateInstance(type)
                    : null;

            public static IFuNode Create() => DispatchProxy.Create<IFuNode, Foreign>();
        }

        [TestMethod]
        public void Children_AreTypedAsIFuNode()
        {
            using Node root = new("root"), a = new("a"), b = new("b");

            root.SetChildren(Spread.Create<IFuNode>(a, b));

            IFuNode node = root;
            Assert.AreSame(root, a.Parent);
            CollectionAssert.AreEqual(new IFuNode[] { a, b }, node.Children.ToArray());
            CollectionAssert.AreEqual(new[] { "a", "b" }, node.Children.Select(child => child.ToString()).ToArray());
        }

        [TestMethod]
        public void SetChildren_Spread_IsStoredAsIs()
        {
            using Node root = new("root"), a = new("a");
            var children = Spread.Create<IFuNode>(a);

            root.SetChildren(children);

            Assert.AreSame(children, root.Children);
        }

        [TestMethod]
        public void SetChildren_SameNodesInAnotherList_KeepsStoredChildren()
        {
            using Node root = new("root"), a = new("a"), b = new("b");
            var children = Spread.Create<IFuNode>(a, b);
            root.SetChildren(children);

            root.SetChildren(new List<IFuNode> { a, b });

            Assert.AreSame(children, root.Children);
        }

        [TestMethod]
        public void SetChildren_ForeignImplementation_IsIgnoredWithWarning()
        {
            using Node root = new("root"), a = new("a");
            var foreign = Foreign.Create();

            root.SetChildren(Spread.Create(a, foreign));

            CollectionAssert.AreEqual(new IFuNode[] { a }, root.Children.ToArray());
            Assert.AreEqual(1, root.Warnings.Count);
            StringAssert.Contains(root.Warnings[0], "only Fu nodes can be children");
        }

        [TestMethod]
        public void SetParent_ForeignParent_IsIgnoredWithWarning()
        {
            using var a = new Node("a");

            a.SetParent(Foreign.Create());

            Assert.IsNull(a.Parent);
            Assert.AreEqual(1, a.Warnings.Count);
            StringAssert.Contains(a.Warnings[0], "doesn't derive from Traversable");
        }
    }
}
