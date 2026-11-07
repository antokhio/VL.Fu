using Fu.Core.Layout;
using Fu.Core.Rendering;
using Fu.Core.Styling;
using Microsoft.Extensions.Logging;
using Stride.Core.Mathematics;
using VL.Lib.Collections;
using YogaSharp;

namespace Fu.Core.Tests.Styling
{
    [TestClass]
    public sealed unsafe class StylableTests
    {
        private sealed class Node : Renderable
        {
            public Node()
                : base(null!) { }

            public YGNode* Native => Handle;

            public int Passes { get; private set; }

            public List<string> Marks { get; } = new();

            public List<string> Warnings { get; } = new();

            public override void Log(LogLevel level, string message, TimeSpan? lifetime = null)
            {
                if (level >= LogLevel.Warning)
                    Warnings.Add(message);
                base.Log(level, message, lifetime);
            }

            protected override void BeginStyle()
            {
                base.BeginStyle();
                Passes++;
                Marks.Clear();
            }
        }

        /// <summary>
        /// A layoutable node that is not a <see cref="Renderable"/>, for instructions of the wrong node type.
        /// </summary>
        private sealed class Plain : Layoutable<Plain>
        {
            public Plain()
                : base(null!) { }

            public List<string> Warnings { get; } = new();

            public override void Log(LogLevel level, string message, TimeSpan? lifetime = null)
            {
                if (level >= LogLevel.Warning)
                    Warnings.Add(message);
                base.Log(level, message, lifetime);
            }
        }

        /// <summary>
        /// A node without Yoga, for Yoga instructions on a node that can't take them.
        /// </summary>
        private sealed class Bare : Stylable<Bare>
        {
            public Bare()
                : base(null!) { }

            public List<string> Warnings { get; } = new();

            public override void Log(LogLevel level, string message, TimeSpan? lifetime = null)
            {
                if (level >= LogLevel.Warning)
                    Warnings.Add(message);
                base.Log(level, message, lifetime);
            }
        }

        /// <summary>
        /// Records the order instructions run in.
        /// </summary>
        private sealed record Mark(Spread<Style> Inputs, string Name) : Style<Node>(Inputs)
        {
            public override void Build(Node node) => node.Marks.Add(Name);
        }

        private sealed record Throwing(Spread<Style> Inputs) : Style(Inputs)
        {
            public override void Build(Fu.Core.Base.NodeBase node) => throw new InvalidOperationException("boom");
        }

        private static Spread<Style> S(params Style[] items) => Spread.Create(items);

        private static Style Combine(params Style[] items) => new CombinedStyle(S(items));

        private static Mark M(string name) => new(S(), name);

        private static Style Tree(string leaf) => Combine(Combine(M("a"), M("b")), M(leaf));

        [TestMethod]
        public void Template_MatchesFreshNode_CopyLeavesNodeClean()
        {
            using var node = new Node();
            node.CalculateLayout(100, 100);
            Assert.IsFalse(node.Native->IsDirty());

            node.Native->CopyStyle(YogaStyleStage.Template);

            Assert.IsFalse(node.Native->IsDirty());
        }

        [TestMethod]
        public void SetStyle_EqualYogaStyle_LeavesNodeClean()
        {
            using var node = new Node();
            node.CalculateLayout(100, 100);

            node.SetStyle(new WidthStyle(S(), YGValue.Auto));

            Assert.AreEqual(YGValue.Auto, node.Native->GetWidth());
            Assert.IsFalse(node.Native->IsDirty());
        }

        [TestMethod]
        public void SetStyle_ChangedYogaStyle_DirtiesNode()
        {
            using var node = new Node();
            node.CalculateLayout(100, 100);

            node.SetStyle(new WidthStyle(S(), YGValue.Point(10)));

            Assert.AreEqual(YGValue.Point(10), node.Native->GetWidth());
            Assert.IsTrue(node.Native->IsDirty());
        }

        [TestMethod]
        public void SetStyle_SameReference_RunsNoPass()
        {
            using var node = new Node();
            var style = M("a");

            node.SetStyle(style);
            node.SetStyle(style);

            Assert.AreEqual(1, node.Passes);
            Assert.AreSame(style, node.Style);
        }

        [TestMethod]
        public void SetStyle_NullOnFreshNode_RunsNoPass()
        {
            using var node = new Node();

            node.SetStyle(null);

            Assert.AreEqual(0, node.Passes);
        }

        [TestMethod]
        public void SetStyle_Chain_RunsInputsFirst()
        {
            using var node = new Node();

            node.SetStyle(new Mark(S(new Mark(S(M("a")), "b")), "c"));

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, node.Marks);
        }

        [TestMethod]
        public void SetStyle_NestedCombine_RunsDepthFirstLeftToRight()
        {
            using var node = new Node();

            node.SetStyle(Combine(Combine(M("a"), M("b")), M("c")));

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, node.Marks);
        }

        [TestMethod]
        public void SetStyle_SharedSubtree_RunsInBothTrees()
        {
            using var first = new Node();
            using var second = new Node();
            var shared = Combine(M("a"), M("b"));

            first.SetStyle(Combine(shared, M("c")));
            second.SetStyle(Combine(M("d"), shared));

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, first.Marks);
            CollectionAssert.AreEqual(new[] { "d", "a", "b" }, second.Marks);
        }

        [TestMethod]
        public void Equals_EqualTrees_AreEqual()
        {
            var first = Tree("c");
            var second = Tree("c");

            Assert.AreNotSame(first, second);
            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [TestMethod]
        public void Equals_ChangedLeaf_Differs()
        {
            Assert.AreNotEqual(Tree("c"), Tree("x"));
            Assert.AreNotEqual(
                new WidthStyle(S(new WidthStyle(S(), YGValue.Point(10))), YGValue.Point(5)),
                new WidthStyle(S(new WidthStyle(S(), YGValue.Point(11))), YGValue.Point(5))
            );
        }

        [TestMethod]
        public void Equals_SameInputsOtherType_Differs()
        {
            Assert.AreNotEqual<Style>(new CombinedStyle(S(M("a"))), new Throwing(S(M("a"))));
        }

        [TestMethod]
        public void SetStyle_LaterSetterWins()
        {
            using var node = new Node();

            node.SetStyle(new WidthStyle(S(new WidthStyle(S(), YGValue.Point(10))), YGValue.Point(5)));

            Assert.AreEqual(YGValue.Point(5), node.Native->GetWidth());
        }

        [TestMethod]
        public void SetStyle_RemovedSetter_FallsBackToEarlierSetter()
        {
            using var node = new Node();
            var ten = new WidthStyle(S(), YGValue.Point(10));
            node.SetStyle(new WidthStyle(S(ten), YGValue.Point(5)));

            node.SetStyle(ten);

            Assert.AreEqual(YGValue.Point(10), node.Native->GetWidth());
        }

        [TestMethod]
        public void SetStyle_Null_RestoresDefaults()
        {
            using var node = new Node();
            node.SetStyle(
                Combine(new WidthStyle(S(), YGValue.Point(10)), new TransformStyle(S(), Matrix.Translation(5, 0, 0)))
            );

            node.SetStyle(null);

            Assert.AreEqual(YGValue.Auto, node.Native->GetWidth());
            Assert.AreEqual(Matrix.Identity, node.Transformation);
        }

        [TestMethod]
        public void SetStyle_Transform_CommitsTransformation()
        {
            using var node = new Node();

            node.SetStyle(new TransformStyle(S(), Matrix.Translation(5, 7, 0)));

            Assert.AreEqual(Matrix.Translation(5, 7, 0), node.Transformation);
        }

        [TestMethod]
        public void SetStyle_UnchangedStyledTransform_KeepsDirectTransformation()
        {
            using var node = new Node();
            node.SetStyle(new WidthStyle(S(), YGValue.Point(10)));
            node.SetTransformation(Matrix.Translation(3, 0, 0));

            node.SetStyle(new WidthStyle(S(), YGValue.Point(20)));

            Assert.AreEqual(Matrix.Translation(3, 0, 0), node.Transformation);
        }

        [TestMethod]
        public void SetStyle_ThrowingInstruction_IsSkippedWithWarning()
        {
            using var node = new Node();

            node.SetStyle(Combine(M("a"), new Throwing(S()), new WidthStyle(S(), YGValue.Point(10)), M("b")));

            CollectionAssert.AreEqual(new[] { "a", "b" }, node.Marks);
            Assert.AreEqual(YGValue.Point(10), node.Native->GetWidth());
            Assert.AreEqual(1, node.Warnings.Count);
            StringAssert.Contains(node.Warnings[0], "Throwing skipped");
            StringAssert.Contains(node.Warnings[0], "boom");
        }

        [TestMethod]
        public void SetStyle_UndefinedWidth_IsSkippedWithWarning()
        {
            using var node = new Node();

            node.SetStyle(new WidthStyle(S(new WidthStyle(S(), YGValue.Point(10))), YGValue.Undefined));

            Assert.AreEqual(YGValue.Point(10), node.Native->GetWidth());
            Assert.AreEqual(1, node.Warnings.Count);
        }

        [TestMethod]
        public void SetStyle_StyleOfOtherNodeType_IsSkippedWithWarning()
        {
            using var node = new Plain();

            node.SetStyle(Combine(new TransformStyle(S(), Matrix.Translation(5, 0, 0)), new WidthStyle(S(), 10)));

            Assert.AreEqual(Matrix.Identity, node.Transformation);
            Assert.AreEqual(1, node.Warnings.Count);
            StringAssert.Contains(node.Warnings[0], "requires a Renderable node");
        }

        [TestMethod]
        public void SetStyle_YogaStyleWithoutYoga_IsSkippedWithWarning()
        {
            using var node = new Bare();

            node.SetStyle(new WidthStyle(S(), 10));

            Assert.AreEqual(1, node.Warnings.Count);
            StringAssert.Contains(node.Warnings[0], "requires a layoutable node");
        }

        [TestMethod]
        public void SetStyle_AfterDispose_DoesNotThrow()
        {
            var node = new Node();
            node.Dispose();

            node.SetStyle(new WidthStyle(S(), 10));
        }
    }
}
