using Fu.Core.Layout;
using Microsoft.Extensions.Logging;
using Stride.Core.Mathematics;
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

            public List<string> Warnings { get; } = new();

            public List<(RectangleF Old, RectangleF New)> LayoutChanges { get; } = new();

            public override void Log(LogLevel level, string message, TimeSpan? lifetime = null)
            {
                if (level >= LogLevel.Warning)
                    Warnings.Add(message);
                base.Log(level, message, lifetime);
            }

            protected override void OnLayoutChanged(RectangleF oldLayout, RectangleF newLayout)
            {
                base.OnLayoutChanged(oldLayout, newLayout);
                LayoutChanges.Add((oldLayout, newLayout));
            }

            public override string ToString() => Name;
        }

        private static void AssertLayout(Box box, float x, float y, float width, float height)
        {
            Assert.AreEqual(new RectangleF(x, y, width, height).ToString(), box.Layout.ToString(), $"layout of {box}");
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

        [TestMethod]
        public void CalculateLayout_Root_AppliesParentRelativeLayoutToTree()
        {
            using Box root = new("root"), b = new("b"), c = new("c"), d = new("d");
            root.SetChildren(S(b, c));
            c.SetChildren(S(d));
            root.Native->SetFlexDirection(YGFlexDirection.Row);
            root.Native->SetWidth(300);
            root.Native->SetHeight(100);
            root.Native->SetPadding(YGEdge.All, 10);
            b.Native->SetWidth(80);
            c.Native->SetFlexGrow(1);
            c.Native->SetPadding(YGEdge.All, 5);
            d.Native->SetHeight(20);

            root.CalculateLayout();

            AssertLayout(root, 0, 0, 300, 100);
            AssertLayout(b, 10, 10, 80, 80);
            AssertLayout(c, 90, 10, 200, 80);
            AssertLayout(d, 5, 5, 190, 20);
        }

        [TestMethod]
        public void CalculateLayout_AvailableSize_SizesUnstyledRoot()
        {
            using Box root = new("root"), b = new("b");
            root.SetChildren(S(b));
            b.Native->SetFlexGrow(1);

            root.CalculateLayout(200, 50);

            AssertLayout(root, 0, 0, 200, 50);
            AssertLayout(b, 0, 0, 200, 50);
        }

        [TestMethod]
        public void CalculateLayout_SiblingResized_MovesChildThatKeptItsSize()
        {
            using Box root = new("root"), b = new("b"), c = new("c"), d = new("d");
            root.SetChildren(S(b, c));
            c.SetChildren(S(d));
            root.Native->SetFlexDirection(YGFlexDirection.Row);
            root.Native->SetWidth(300);
            root.Native->SetHeight(100);
            b.Native->SetWidth(100);
            c.Native->SetWidth(50);
            d.Native->SetHeight(10);
            root.CalculateLayout();

            b.Native->SetWidth(150);
            root.CalculateLayout();

            AssertLayout(b, 0, 0, 150, 100);
            AssertLayout(c, 150, 0, 50, 100);
            AssertLayout(d, 0, 0, 50, 10);
        }

        [TestMethod]
        public void CalculateLayout_ReportsOnlyNodesThatChanged()
        {
            using Box root = new("root"), b = new("b"), c = new("c"), d = new("d");
            root.SetChildren(S(b, c));
            c.SetChildren(S(d));
            root.Native->SetFlexDirection(YGFlexDirection.Row);
            root.Native->SetWidth(300);
            root.Native->SetHeight(100);
            b.Native->SetWidth(100);
            c.Native->SetWidth(50);
            d.Native->SetHeight(10);
            root.CalculateLayout();
            foreach (var box in new[] { root, b, c, d })
            {
                Assert.AreEqual(1, box.LayoutChanges.Count, $"first pass of {box}");
                box.LayoutChanges.Clear();
            }

            b.Native->SetWidth(150);
            root.CalculateLayout();

            Assert.AreEqual(0, root.LayoutChanges.Count);
            Assert.AreEqual(1, b.LayoutChanges.Count);
            Assert.AreEqual(new RectangleF(0, 0, 100, 100).ToString(), b.LayoutChanges[0].Old.ToString());
            Assert.AreEqual(new RectangleF(0, 0, 150, 100).ToString(), b.LayoutChanges[0].New.ToString());
            Assert.AreEqual(1, c.LayoutChanges.Count);
            Assert.AreEqual(0, d.LayoutChanges.Count);
        }

        [TestMethod]
        public void CalculateLayout_Unchanged_DoesNotReportAgain()
        {
            using Box root = new("root"), b = new("b");
            root.SetChildren(S(b));
            b.Native->SetFlexGrow(1);
            root.CalculateLayout(200, 50);
            root.LayoutChanges.Clear();
            b.LayoutChanges.Clear();

            root.CalculateLayout(200, 50);

            Assert.AreEqual(0, root.LayoutChanges.Count);
            Assert.AreEqual(0, b.LayoutChanges.Count);
        }

        [TestMethod]
        public void CalculateLayout_AvailableSizeChanged_UpdatesLayout()
        {
            using Box root = new("root"), b = new("b");
            root.SetChildren(S(b));
            b.Native->SetFlexGrow(1);
            root.CalculateLayout(200, 50);

            root.CalculateLayout(120, 40);

            AssertLayout(root, 0, 0, 120, 40);
            AssertLayout(b, 0, 0, 120, 40);
        }

        [TestMethod]
        public void CalculateLayout_NonRoot_WarnsAndDoesNothing()
        {
            using Box root = new("root"), b = new("b");
            root.SetChildren(S(b));
            b.Native->SetWidth(40);
            b.Native->SetHeight(30);

            b.CalculateLayout(100, 100);

            Assert.AreEqual(1, b.Warnings.Count);
            Assert.AreEqual(0, b.LayoutChanges.Count);
            Assert.AreEqual(RectangleF.Empty, b.Layout);
            Assert.AreEqual(0, root.Warnings.Count);
        }

        [TestMethod]
        public void CalculateLayout_Disposed_IsIgnored()
        {
            var a = new Box("a");
            a.Dispose();

            a.CalculateLayout(100, 100);

            Assert.AreEqual(0, a.Warnings.Count);
            Assert.AreEqual(RectangleF.Empty, a.Layout);
        }
    }
}
