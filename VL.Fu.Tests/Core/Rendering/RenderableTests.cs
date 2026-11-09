using VL.Fu.Core.Rendering;
using Stride.Core.Mathematics;
using VL.Lib.Collections;
using YogaSharp;

namespace VL.Fu.Tests.Core.Rendering
{
    [TestClass]
    public sealed unsafe class RenderableTests
    {
        private const float Tolerance = 1e-4f;

        private sealed class Node : Renderable<Node>
        {
            public Node(string name)
                : base(null!)
            {
                Name = name;
            }

            public string Name { get; }

            public YGNode* Native => Handle;

            public override string ToString() => Name;
        }

        private static void AssertRect(RectangleF? actual, float x, float y, float width, float height)
        {
            Assert.IsNotNull(actual);
            var r = actual.Value;
            Assert.AreEqual(x, r.X, Tolerance, $"x of {r}");
            Assert.AreEqual(y, r.Y, Tolerance, $"y of {r}");
            Assert.AreEqual(width, r.Width, Tolerance, $"width of {r}");
            Assert.AreEqual(height, r.Height, Tolerance, $"height of {r}");
        }

        /// <summary>
        /// A 300 x 100 row with 10 padding, holding an 80 wide child and a 50 wide child.
        /// </summary>
        private static (Node Root, Node B, Node C) CreateRow()
        {
            Node root = new("root"), b = new("b"), c = new("c");
            root.SetChildren(Spread.Create(new[] { b, c }));
            root.Native->SetFlexDirection(YGFlexDirection.Row);
            root.Native->SetWidth(300);
            root.Native->SetHeight(100);
            root.Native->SetPadding(YGEdge.All, 10);
            b.Native->SetWidth(80);
            c.Native->SetWidth(50);
            return (root, b, c);
        }

        [TestMethod]
        public void Bounds_BeforeLayout_IsNull()
        {
            using var a = new Node("a");

            Assert.IsNull(a.Bounds);
        }

        [TestMethod]
        public void Bounds_TransformationBeforeLayout_StaysNull()
        {
            using var a = new Node("a");

            a.SetTransformation(Matrix.Translation(5, 7, 0));

            Assert.IsNull(a.Bounds);
        }

        [TestMethod]
        public void Bounds_EmptyNodeLaidOut_IsKnown()
        {
            using var a = new Node("a");

            a.CalculateLayout();

            AssertRect(a.Bounds, 0, 0, 0, 0);
        }

        [TestMethod]
        public void Bounds_AfterLayout_IsLayoutInParentSpace()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                root.CalculateLayout();

                AssertRect(root.Bounds, 0, 0, 300, 100);
                AssertRect(b.Bounds, 10, 10, 80, 80);
                AssertRect(c.Bounds, 90, 10, 50, 80);
            }
        }

        [TestMethod]
        public void Bounds_Translation_MovesBoundsButNotLayout()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                root.CalculateLayout();

                b.SetTransformation(Matrix.Translation(5, 7, 0));

                AssertRect(b.Bounds, 15, 17, 80, 80);
                AssertRect(b.Layout, 10, 10, 80, 80);
                AssertRect(c.Bounds, 90, 10, 50, 80);
            }
        }

        [TestMethod]
        public void Bounds_Rotation_PivotsAroundTopLeft()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                root.CalculateLayout();

                b.SetTransformation(Matrix.RotationZ(MathUtil.PiOverTwo));

                AssertRect(b.Bounds, -70, 10, 80, 80);
            }
        }

        [TestMethod]
        public void Bounds_Scaling_GrowsFromTopLeft()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                root.CalculateLayout();

                b.SetTransformation(Matrix.Scaling(2, 0.5f, 1));

                AssertRect(b.Bounds, 10, 10, 160, 40);
            }
        }

        [TestMethod]
        public void Bounds_TransformationSetBeforeLayout_IsKeptAfterLayout()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                b.SetTransformation(Matrix.Translation(5, 7, 0));

                root.CalculateLayout();

                AssertRect(b.Bounds, 15, 17, 80, 80);
            }
        }

        [TestMethod]
        public void Bounds_Relayout_FollowsNewPosition()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                c.SetTransformation(Matrix.Translation(0, 5, 0));
                root.CalculateLayout();

                b.Native->SetWidth(120);
                root.CalculateLayout();

                AssertRect(c.Bounds, 130, 15, 50, 80);
            }
        }
    }
}
