using Fu.Core.Tree;
using Microsoft.Extensions.Logging;
using VL.Lib.Collections;

namespace Fu.Core.Tests.Tree
{
    [TestClass]
    public sealed class TraversableTests
    {
        // No NodeContext: there is no vvvv app host in unit tests, so messages are only captured here.
        private sealed class Node : Traversable<Node>
        {
            public Node(string name, List<string>? events = null)
                : base(null!)
            {
                Name = name;
                Events = events ?? new List<string>();
            }

            public string Name { get; }

            public List<string> Events { get; }

            public List<string> Warnings { get; } = new();

            public override void Log(LogLevel level, string message, TimeSpan? lifetime = null)
            {
                if (level >= LogLevel.Warning)
                    Warnings.Add(message);
                base.Log(level, message, lifetime);
            }

            protected override void OnParentChanged(Node? oldParent, Node? newParent)
                => Events.Add($"{Name}.parent {oldParent?.Name ?? "-"}>{newParent?.Name ?? "-"}");

            protected override void OnChildrenChanged(Spread<Node> oldChildren, Spread<Node> newChildren)
                => Events.Add($"{Name}.children [{Names(oldChildren)}]>[{Names(newChildren)}]");

            public override string ToString() => Name;
        }

        private sealed class Other : Traversable<Other>
        {
            public Other() : base(null!) { }
        }

        private sealed class Mismatched : Traversable<Other>
        {
            public Mismatched() : base(null!) { }
        }

        private static string Names(IEnumerable<Node> nodes) => string.Join(",", nodes.Select(n => n.Name));

        private static Spread<Node> S(params Node[] nodes) => Spread.Create(nodes);

        [TestMethod]
        public void SetChildren_LinksParentAndKeepsOrder()
        {
            Node a = new("a"), b = new("b"), c = new("c");

            a.SetChildren(S(b, c));

            Assert.AreEqual("b,c", Names(a.Children));
            Assert.AreSame(a, b.Parent);
            Assert.AreSame(a, c.Parent);
            Assert.IsNull(a.Parent);
        }

        [TestMethod]
        public void SetChildren_DetachesRemovedChildren()
        {
            Node a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(b, c));

            a.SetChildren(S(c));

            Assert.IsNull(b.Parent);
            Assert.AreSame(a, c.Parent);
            Assert.AreEqual("c", Names(a.Children));
        }

        [TestMethod]
        public void SetChildren_TakesChildFromPreviousParent()
        {
            Node a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(c));

            b.SetChildren(S(c));

            Assert.AreSame(b, c.Parent);
            Assert.AreEqual(0, a.Children.Count);
            Assert.AreEqual("c", Names(b.Children));
        }

        [TestMethod]
        public void SetChildren_Null_ClearsChildren()
        {
            Node a = new("a"), b = new("b");
            a.SetChildren(S(b));

            a.SetChildren(null!);

            Assert.AreEqual(0, a.Children.Count);
            Assert.IsNull(b.Parent);
        }

        [TestMethod]
        public void SetParent_AppendsAsLastChild()
        {
            Node a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(b));

            c.SetParent(a);

            Assert.AreEqual("b,c", Names(a.Children));
            Assert.AreSame(a, c.Parent);
        }

        [TestMethod]
        public void SetParent_MovesBetweenParents()
        {
            Node a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(c));

            c.SetParent(b);

            Assert.AreSame(b, c.Parent);
            Assert.AreEqual(0, a.Children.Count);
            Assert.AreEqual("c", Names(b.Children));
        }

        [TestMethod]
        public void SetParent_Null_Detaches()
        {
            Node a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(b, c));

            b.SetParent(null);

            Assert.IsNull(b.Parent);
            Assert.AreEqual("c", Names(a.Children));
        }

        [TestMethod]
        public void SetParent_Null_OnRoot_IsNoOp()
        {
            var log = new List<string>();
            var a = new Node("a", log);

            a.SetParent(null);

            Assert.IsNull(a.Parent);
            Assert.AreEqual(0, log.Count);
        }

        [TestMethod]
        public void SetChildren_SameNodes_DoesNotRaiseHooks()
        {
            var log = new List<string>();
            Node a = new("a", log), b = new("b", log), c = new("c", log);
            a.SetChildren(S(b, c));
            log.Clear();

            a.SetChildren(S(b, c));

            Assert.AreEqual(0, log.Count);
        }

        [TestMethod]
        public void SetChildren_Reorder_RaisesOnlyChildrenChanged()
        {
            var log = new List<string>();
            Node a = new("a", log), b = new("b", log), c = new("c", log);
            a.SetChildren(S(b, c));
            log.Clear();

            a.SetChildren(S(c, b));

            CollectionAssert.AreEqual(new[] { "a.children [b,c]>[c,b]" }, log);
        }

        [TestMethod]
        public void Hooks_ReportOldAndNewValues_WhenChildMoves()
        {
            var log = new List<string>();
            Node a = new("a", log), b = new("b", log), c = new("c", log);
            a.SetChildren(S(c));
            log.Clear();

            b.SetChildren(S(c));

            CollectionAssert.AreEqual(
                new[]
                {
                    "a.children [c]>[]",
                    "c.parent a>b",
                    "b.children []>[c]",
                },
                log);
        }

        [TestMethod]
        public void SetChildren_Self_IsIgnoredAndWarned()
        {
            var a = new Node("a");

            a.SetChildren(S(a));

            Assert.AreEqual(0, a.Children.Count);
            Assert.IsNull(a.Parent);
            CollectionAssert.AreEqual(
                new[] { "Ignored 'a': a node can't be a child of itself or of its own descendants." },
                a.Warnings);
        }

        [TestMethod]
        public void SetChildren_Ancestor_IsIgnoredKeepsValidChildrenAndWarns()
        {
            Node a = new("a"), b = new("b"), c = new("c"), d = new("d");
            a.SetChildren(S(b));
            b.SetChildren(S(c));

            c.SetChildren(S(a, d));

            Assert.IsNull(a.Parent);
            Assert.AreEqual("d", Names(c.Children));
            Assert.AreSame(c, d.Parent);
            Assert.AreEqual(1, c.Warnings.Count);
            StringAssert.Contains(c.Warnings[0], "'a'");
        }

        [TestMethod]
        public void SetChildren_Duplicate_KeepsFirstAndWarns()
        {
            Node a = new("a"), b = new("b"), c = new("c");

            a.SetChildren(S(b, c, b));

            Assert.AreEqual("b,c", Names(a.Children));
            Assert.AreSame(a, b.Parent);
            CollectionAssert.AreEqual(
                new[] { "Ignored 'b': it is already a child of this node." },
                a.Warnings);
        }

        [TestMethod]
        public void SetChildren_NullEntry_IsSkippedAndWarned()
        {
            Node a = new("a"), b = new("b");

            a.SetChildren(S(null!, b));

            Assert.AreEqual("b", Names(a.Children));
            CollectionAssert.AreEqual(new[] { "Ignored an empty child." }, a.Warnings);
        }

        [TestMethod]
        public void SetChildren_SeveralProblems_AreReportedInOneWarning()
        {
            Node a = new("a"), b = new("b");

            a.SetChildren(S(null!, b, b, a));

            Assert.AreEqual("b", Names(a.Children));
            Assert.AreEqual(1, a.Warnings.Count);
            Assert.AreEqual(3, a.Warnings[0].Split(Environment.NewLine).Length);
        }

        [TestMethod]
        public void SetChildren_RepeatedInvalidInput_DoesNotRaiseHooksAgain()
        {
            var events = new List<string>();
            Node a = new("a", events), b = new("b", events);
            a.SetChildren(S(b, b));
            events.Clear();

            a.SetChildren(S(b, b));

            Assert.AreEqual(0, events.Count);
            Assert.AreEqual("b", Names(a.Children));
        }

        [TestMethod]
        public void SetChildren_ValidInput_DoesNotWarn()
        {
            Node a = new("a"), b = new("b"), c = new("c");

            a.SetChildren(S(b, c));

            Assert.AreEqual(0, a.Warnings.Count);
        }

        [TestMethod]
        public void SetParent_UnderOwnDescendant_IsIgnoredAndWarned()
        {
            Node a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(b));
            b.SetChildren(S(c));

            a.SetParent(c);

            Assert.IsNull(a.Parent);
            Assert.AreEqual(0, c.Children.Count);
            Assert.AreEqual(1, a.Warnings.Count);
            Assert.AreEqual(0, c.Warnings.Count);
        }

        [TestMethod]
        public void SetParent_Self_IsIgnoredAndWarned()
        {
            var a = new Node("a");

            a.SetParent(a);

            Assert.IsNull(a.Parent);
            Assert.AreEqual(0, a.Children.Count);
            Assert.AreEqual(1, a.Warnings.Count);
        }

        [TestMethod]
        public void Descendants_ArePreOrder()
        {
            Node a = new("a"), b = new("b"), c = new("c"), d = new("d"), e = new("e");
            a.SetChildren(S(b, d));
            b.SetChildren(S(c));
            d.SetChildren(S(e));

            Assert.AreEqual("b,c,d,e", Names(a.Descendants()));
        }

        [TestMethod]
        public void RootAncestorsAndIsAncestorOf_WalkUp()
        {
            Node a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(b));
            b.SetChildren(S(c));

            Assert.AreSame(a, c.Root());
            Assert.AreEqual("b,a", Names(c.Ancestors()));
            Assert.IsTrue(a.IsAncestorOf(c));
            Assert.IsFalse(c.IsAncestorOf(a));
            Assert.IsFalse(c.IsAncestorOf(c));
        }

        [TestMethod]
        public void Enumerating_YieldsDirectChildren()
        {
            Node a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(b, c));

            Assert.AreEqual("b,c", Names(a));
        }

        [TestMethod]
        public void UntypedView_ExposesSameTree()
        {
            Node a = new("a"), b = new("b");
            a.SetChildren(S(b));

            ITraversable untyped = a;

            Assert.AreEqual(1, untyped.Children.Count);
            Assert.AreSame(b, untyped.Children[0]);
            Assert.AreSame(a, untyped.Children[0].Parent);
        }

        [TestMethod]
        public void Constructor_MismatchedSelfType_Throws()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => new Mismatched());
        }
    }
}
