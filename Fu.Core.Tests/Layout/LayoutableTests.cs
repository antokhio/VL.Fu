using Fu.Core.Layout;
using VL.Lib.Collections;
using YogaSharp;

namespace Fu.Core.Tests.Layout
{
    [TestClass]
    public sealed unsafe class LayoutableTests
    {
        private sealed class Box : Layoutable<Box>
        {
            public Box(string name)
                : base(null!)
            {
                Name = name;
            }

            public string Name { get; }

            public YGNode* Native => Handle;

            public override string ToString() => Name;
        }

        private static Spread<Box> S(params Box[] boxes) => Spread.Create(boxes);

        private static void AssertNativeChildren(Box parent, params Box[] expected)
        {
            Assert.AreEqual(expected.Length, parent.Native->GetChildCount(), $"native child count of {parent}");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.IsTrue(parent.Native->GetChild(i) == expected[i].Native, $"native child {i} of {parent}");
                Assert.IsTrue(expected[i].Native->GetOwner() == parent.Native, $"native owner of {expected[i]}");
            }
        }

        [TestMethod]
        public void SetChildren_MirrorsChildrenAndOrderIntoYoga()
        {
            using Box a = new("a"), b = new("b"), c = new("c");

            a.SetChildren(S(b, c));
            AssertNativeChildren(a, b, c);

            a.SetChildren(S(c, b));
            AssertNativeChildren(a, c, b);
        }

        [TestMethod]
        public void SetChildren_RemovedChild_LosesNativeOwner()
        {
            using Box a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(b, c));

            a.SetChildren(S(c));

            AssertNativeChildren(a, c);
            Assert.IsTrue(b.Native->GetOwner() == null);
        }

        [TestMethod]
        public void SetChildren_ChildTakenByOtherParent_IsRemovedFromOldNativeParent()
        {
            using Box a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(c));

            b.SetChildren(S(c));

            AssertNativeChildren(a);
            AssertNativeChildren(b, c);
        }

        [TestMethod]
        public void SetParent_MovesNativeNode()
        {
            using Box a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(c));

            c.SetParent(b);
            AssertNativeChildren(a);
            AssertNativeChildren(b, c);

            c.SetParent(null);
            AssertNativeChildren(b);
            Assert.IsTrue(c.Native->GetOwner() == null);
        }

        [TestMethod]
        public void SetChildren_InvalidEntries_AreNotMirrored()
        {
            using Box a = new("a"), b = new("b");

            a.SetChildren(S(b, b, a));

            AssertNativeChildren(a, b);
        }

        [TestMethod]
        public void Dispose_Child_DetachesFromParentAndFreesHandle()
        {
            using Box a = new("a"), c = new("c");
            var b = new Box("b");
            a.SetChildren(S(b, c));

            b.Dispose();

            Assert.AreEqual("c", string.Join(",", a.Children.Select(x => x.Name)));
            AssertNativeChildren(a, c);
            Assert.IsTrue(b.Native == null);
        }

        [TestMethod]
        public void Dispose_Parent_ReleasesChildrenButKeepsTheirHandles()
        {
            using Box b = new("b"), c = new("c");
            var a = new Box("a");
            a.SetChildren(S(b, c));

            a.Dispose();

            Assert.IsNull(b.Parent);
            Assert.IsNull(c.Parent);
            Assert.IsTrue(b.Native != null && b.Native->GetOwner() == null);
            Assert.IsTrue(c.Native != null && c.Native->GetOwner() == null);
        }

        [TestMethod]
        public void Dispose_Twice_IsSafe()
        {
            var a = new Box("a");

            a.Dispose();
            a.Dispose();

            Assert.IsTrue(a.Native == null);
        }
    }
}
