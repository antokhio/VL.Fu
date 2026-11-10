using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Core.Rendering;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;
using VL.Skia;
using YogaSharp;

namespace VL.Fu.Tests.Core.Rendering
{
    /// <summary>
    /// Pixel checks for overflow clipping. A 20 x 20 root is moved to (10, 10) on a 60 x 60 bitmap,
    /// and its 40 x 40 red child overflows it to the right and bottom.
    /// </summary>
    [TestClass]
    public sealed unsafe class RenderableClippingTests
    {
        private sealed class Node : Renderable<Node>
        {
            public Node()
                : base(null!) { }

            public YGNode* Native => Handle;
        }

        /// <summary>
        /// Fills a rectangle in the node's own space, without antialiasing, so only the clip can soften an edge.
        /// </summary>
        private sealed class FillLayer(SKRect rect, SKColor color) : ILayer
        {
            public RectangleF? Bounds => null;

            public void Render(CallerInfo caller)
            {
                using var paint = new SKPaint { Color = color, IsAntialias = false };
                caller.Canvas.DrawRect(rect, paint);
            }

            public bool Notify(INotification notification, CallerInfo caller) => false;
        }

        private static (Node Root, Node Child) CreateScene(YGOverflow overflow, float border = 0)
        {
            Node root = new(), child = new();
            root.SetChildren(Spread.Create(new[] { child }));
            root.Native->SetWidth(20);
            root.Native->SetHeight(20);
            root.Native->SetBorder(YGEdge.All, border);
            root.Native->SetOverflow(overflow);
            child.Native->SetWidth(40);
            child.Native->SetHeight(40);
            child.SetLayer(new FillLayer(SKRect.Create(0, 0, 40, 40), SKColors.Red));
            root.CalculateLayout();
            root.SetTransformation(Matrix.Translation(10, 10, 0));
            return (root, child);
        }

        private static SKBitmap Render(Node root)
        {
            var bitmap = new SKBitmap(60, 60);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            root.Render(CallerInfo.InRenderer(60, 60, canvas, null!, 1f));
            return bitmap;
        }

        private static void AssertRed(SKBitmap bitmap, int x, int y) =>
            Assert.AreEqual(SKColors.Red, bitmap.GetPixel(x, y), $"pixel ({x}, {y})");

        private static void AssertClear(SKBitmap bitmap, int x, int y) =>
            Assert.AreEqual(0, bitmap.GetPixel(x, y).Alpha, $"alpha of pixel ({x}, {y})");

        [TestMethod]
        public void Visible_DoesNotClipChildren()
        {
            var (root, child) = CreateScene(YGOverflow.Visible);
            using (root) using (child)
            {
                using var bitmap = Render(root);

                AssertRed(bitmap, 15, 15);
                AssertRed(bitmap, 45, 45);
            }
        }

        [TestMethod]
        public void Hidden_ClipsChildrenAtEdge()
        {
            var (root, child) = CreateScene(YGOverflow.Hidden);
            using (root) using (child)
            {
                using var bitmap = Render(root);

                AssertRed(bitmap, 15, 15);
                AssertRed(bitmap, 29, 29);
                AssertClear(bitmap, 30, 15);
                AssertClear(bitmap, 15, 30);
                AssertClear(bitmap, 45, 45);
            }
        }

        [TestMethod]
        public void Scroll_ClipsLikeHidden()
        {
            var (root, child) = CreateScene(YGOverflow.Scroll);
            using (root) using (child)
            {
                using var bitmap = Render(root);

                AssertRed(bitmap, 15, 15);
                AssertClear(bitmap, 45, 45);
            }
        }

        [TestMethod]
        public void Hidden_DoesNotClipOwnLayer()
        {
            var (root, child) = CreateScene(YGOverflow.Hidden);
            using (root) using (child)
            {
                root.SetLayer(new FillLayer(SKRect.Create(-5, -5, 30, 30), SKColors.Blue));

                using var bitmap = Render(root);

                Assert.AreEqual(SKColors.Blue, bitmap.GetPixel(7, 7));
            }
        }

        [TestMethod]
        public void Hidden_WithBorder_ClipsAtPaddingBox()
        {
            var (root, child) = CreateScene(YGOverflow.Hidden, border: 4);
            using (root) using (child)
            {
                child.SetTransformation(Matrix.Translation(-4, -4, 0));

                using var bitmap = Render(root);

                AssertClear(bitmap, 12, 12);
                AssertRed(bitmap, 15, 15);
                AssertRed(bitmap, 25, 25);
                AssertClear(bitmap, 27, 27);
            }
        }

        [TestMethod]
        public void OverflowChangedByStyle_ClipsWithoutNewLayout()
        {
            var (root, child) = CreateScene(YGOverflow.Visible);
            using (root) using (child)
            {
                root.SetStyle(new OverflowStyle(Array.Empty<IStyle>(), YGOverflow.Hidden));

                using var bitmap = Render(root);

                AssertClear(bitmap, 45, 45);
            }
        }

        [TestMethod]
        public void Hidden_Rotated_ClipsAlongRotatedEdges()
        {
            var (root, child) = CreateScene(YGOverflow.Hidden);
            using (root) using (child)
            {
                root.SetTransformation(Matrix.RotationZ(MathUtil.PiOverFour) * Matrix.Translation(10, 10, 0));

                using var bitmap = Render(root);

                AssertRed(bitmap, 20, 20);
                AssertClear(bitmap, 29, 29);
            }
        }

        [TestMethod]
        public void Hidden_AxisAlignedFractionalEdge_HasNoPartialPixels()
        {
            var (root, child) = CreateScene(YGOverflow.Hidden);
            using (root) using (child)
            {
                root.SetTransformation(Matrix.Translation(10.25f, 10.25f, 0));

                using var bitmap = Render(root);

                var alpha = bitmap.GetPixel(30, 15).Alpha;
                Assert.IsTrue(alpha is 0 or 255, $"alpha {alpha} at the clip edge");
            }
        }

        [TestMethod]
        public void Hidden_Nested_ClipsByBoth()
        {
            var (root, child) = CreateScene(YGOverflow.Hidden);
            using (root) using (child)
            using (var grandchild = new Node())
            {
                child.Native->SetOverflow(YGOverflow.Hidden);
                child.Native->SetWidth(15);
                grandchild.Native->SetWidth(40);
                grandchild.Native->SetHeight(40);
                grandchild.SetLayer(new FillLayer(SKRect.Create(0, 0, 40, 40), SKColors.Red));
                child.SetLayer(null!);
                child.SetChildren(Spread.Create(new[] { grandchild }));
                root.CalculateLayout();

                using var bitmap = Render(root);

                AssertRed(bitmap, 15, 15);
                AssertClear(bitmap, 27, 15);
                AssertClear(bitmap, 15, 45);
            }
        }

        [TestMethod]
        public void Hidden_BorderWiderThanBox_DrawsNoChildren()
        {
            var (root, child) = CreateScene(YGOverflow.Hidden, border: 15);
            using (root) using (child)
            {
                using var bitmap = Render(root);

                for (var y = 0; y < 60; y += 5)
                    for (var x = 0; x < 60; x += 5)
                        AssertClear(bitmap, x, y);
            }
        }
    }
}
