using VL.Fu.Core.Rendering;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;
using VL.Skia;
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

            public List<string> Warnings { get; } = new();

            public int LayoutChanges { get; private set; }

            public void LayoutRoot() => CalculateRootLayout();

            public override void Log(LogLevel level, string message, TimeSpan? lifetime = null)
            {
                if (level >= LogLevel.Warning)
                    Warnings.Add(message);
                base.Log(level, message, lifetime);
            }

            protected override void OnLayoutChanged(RectangleF oldLayout, RectangleF newLayout)
            {
                base.OnLayoutChanged(oldLayout, newLayout);
                LayoutChanges++;
            }

            public override string ToString() => Name;
        }

        /// <summary>
        /// Records each render: its name, the caller transformation and the canvas matrix at that moment.
        /// </summary>
        private sealed class RecordingLayer(string name, List<(string Name, SKMatrix Caller, SKMatrix Canvas)> log) : ILayer
        {
            public RectangleF? Bounds => null;

            public void Render(CallerInfo caller) => log.Add((name, caller.Transformation, caller.Canvas.TotalMatrix));

            public bool Notify(INotification notification, CallerInfo caller) => false;
        }

        private static CallerInfo CreateCaller(SKCanvas canvas) => CallerInfo.InRenderer(300, 100, canvas, null!, 1f);

        private static void AssertTranslation(SKMatrix m, float x, float y)
        {
            Assert.AreEqual(x, m.TransX, Tolerance, $"x of {m}");
            Assert.AreEqual(y, m.TransY, Tolerance, $"y of {m}");
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

        [TestMethod]
        public void Render_BeforeLayout_DrawsNothing()
        {
            using var a = new Node("a");
            var log = new List<(string Name, SKMatrix Caller, SKMatrix Canvas)>();
            a.SetLayer(new RecordingLayer("a", log));
            using var bitmap = new SKBitmap(1, 1);
            using var canvas = new SKCanvas(bitmap);

            a.Render(CreateCaller(canvas));

            Assert.AreEqual(0, log.Count);
        }

        /// <summary>
        /// Own layer first, then the children on top; each in its own space, with canvas and caller in sync.
        /// </summary>
        [TestMethod]
        public void Render_DrawsLayerThenChildrenInTheirOwnSpace()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                var log = new List<(string Name, SKMatrix Caller, SKMatrix Canvas)>();
                root.SetLayer(new RecordingLayer("root", log));
                b.SetLayer(new RecordingLayer("b", log));
                c.SetLayer(new RecordingLayer("c", log));
                root.SetTransformation(Matrix.Translation(100, 0, 0));
                root.CalculateLayout();
                using var bitmap = new SKBitmap(1, 1);
                using var canvas = new SKCanvas(bitmap);

                root.Render(CreateCaller(canvas));

                CollectionAssert.AreEqual(new[] { "root", "b", "c" }, log.Select(l => l.Name).ToArray());
                AssertTranslation(log[0].Caller, 100, 0);
                AssertTranslation(log[1].Caller, 110, 10);
                AssertTranslation(log[2].Caller, 190, 10);
                foreach (var entry in log)
                    Assert.AreEqual(entry.Caller, entry.Canvas, $"canvas of {entry.Name}");
            }
        }

        [TestMethod]
        public void Render_RestoresCanvas()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                root.SetTransformation(Matrix.Translation(100, 0, 0));
                root.CalculateLayout();
                using var bitmap = new SKBitmap(1, 1);
                using var canvas = new SKCanvas(bitmap);
                var saves = canvas.SaveCount;

                root.Render(CreateCaller(canvas));

                Assert.AreEqual(saves, canvas.SaveCount);
                Assert.AreEqual(SKMatrix.Identity, canvas.TotalMatrix);
            }
        }

        [TestMethod]
        public void Render_NoLayer_StillDrawsChildren()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                var log = new List<(string Name, SKMatrix Caller, SKMatrix Canvas)>();
                c.SetLayer(new RecordingLayer("c", log));
                root.CalculateLayout();
                using var bitmap = new SKBitmap(1, 1);
                using var canvas = new SKCanvas(bitmap);

                root.Render(CreateCaller(canvas));

                Assert.AreEqual(1, log.Count);
                AssertTranslation(log[0].Caller, 90, 10);
            }
        }

        [TestMethod]
        public void Render_DisplayNone_SkipsNodeAndLaysOutWithoutIt()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                var log = new List<(string Name, SKMatrix Caller, SKMatrix Canvas)>();
                b.SetLayer(new RecordingLayer("b", log));
                c.SetLayer(new RecordingLayer("c", log));
                b.Native->SetDisplay(YGDisplay.None);
                root.CalculateLayout();
                using var bitmap = new SKBitmap(1, 1);
                using var canvas = new SKCanvas(bitmap);

                root.Render(CreateCaller(canvas));

                CollectionAssert.AreEqual(new[] { "c" }, log.Select(l => l.Name).ToArray());
                AssertTranslation(log[0].Caller, 10, 10);
            }
        }

        [TestMethod]
        public void Render_DisplayNone_SkipsSubtree()
        {
            Node root = new("root"), b = new("b"), d = new("d");
            using (root) using (b) using (d)
            {
                root.SetChildren(Spread.Create(new[] { b }));
                b.SetChildren(Spread.Create(new[] { d }));
                d.Native->SetWidth(10);
                d.Native->SetHeight(10);
                var log = new List<(string Name, SKMatrix Caller, SKMatrix Canvas)>();
                root.SetLayer(new RecordingLayer("root", log));
                d.SetLayer(new RecordingLayer("d", log));
                b.Native->SetDisplay(YGDisplay.None);
                root.CalculateLayout();
                using var bitmap = new SKBitmap(1, 1);
                using var canvas = new SKCanvas(bitmap);

                root.Render(CreateCaller(canvas));

                CollectionAssert.AreEqual(new[] { "root" }, log.Select(l => l.Name).ToArray());
            }
        }

        /// <summary>
        /// Renders once on a 1 x 1 canvas, only to let a root capture the caller's viewport.
        /// </summary>
        private static void RenderOnce(Node node, Func<CallerInfo, CallerInfo>? adjust = null)
        {
            using var bitmap = new SKBitmap(1, 1);
            using var canvas = new SKCanvas(bitmap);
            var caller = CreateCaller(canvas);
            node.Render(adjust?.Invoke(caller) ?? caller);
        }

        [TestMethod]
        public void CalculateRootLayout_NeverRendered_DoesNothing()
        {
            using var a = new Node("a");

            a.LayoutRoot();

            Assert.IsNull(a.Bounds);
        }

        [TestMethod]
        public void CalculateRootLayout_AfterRender_StretchesToViewport()
        {
            using var a = new Node("a");
            RenderOnce(a);

            a.LayoutRoot();

            AssertRect(a.Layout, 0, 0, 300, 100);
            AssertRect(a.Bounds, 0, 0, 300, 100);
        }

        [TestMethod]
        public void CalculateRootLayout_WidthStyle_WinsOverViewport()
        {
            using var a = new Node("a");
            a.Native->SetWidth(120);
            RenderOnce(a);

            a.LayoutRoot();

            AssertRect(a.Layout, 0, 0, 120, 100);
        }

        [TestMethod]
        public void CalculateRootLayout_PercentWidth_ResolvesAgainstViewport()
        {
            using var a = new Node("a");
            a.Native->SetWidthPercent(50);
            RenderOnce(a);

            a.LayoutRoot();

            AssertRect(a.Layout, 0, 0, 150, 100);
        }

        /// <summary>
        /// A centred space (like DIP) puts the caller's origin in the middle of the viewport;
        /// the root still starts at the viewport's top-left.
        /// </summary>
        [TestMethod]
        public void CalculateRootLayout_CentredCaller_PlacesRootAtViewportTopLeft()
        {
            using var a = new Node("a");
            var log = new List<(string Name, SKMatrix Caller, SKMatrix Canvas)>();
            a.SetLayer(new RecordingLayer("a", log));
            Func<CallerInfo, CallerInfo> centred = c => c.WithTransformation(SKMatrix.CreateTranslation(150, 50));
            RenderOnce(a, centred);

            a.LayoutRoot();
            RenderOnce(a, centred);

            AssertRect(a.Layout, 0, 0, 300, 100);
            AssertRect(a.Bounds, -150, -50, 300, 100);
            Assert.AreEqual(1, log.Count);
            AssertTranslation(log[0].Caller, 0, 0);
        }

        [TestMethod]
        public void CalculateRootLayout_SameViewportAgain_ChangesNothing()
        {
            using var a = new Node("a");
            RenderOnce(a);
            a.LayoutRoot();
            var changes = a.LayoutChanges;

            RenderOnce(a);
            a.LayoutRoot();

            Assert.AreEqual(changes, a.LayoutChanges);
        }

        [TestMethod]
        public void CalculateRootLayout_SingularCaller_KeepsLastViewport()
        {
            using var a = new Node("a");
            RenderOnce(a);

            RenderOnce(a, c => c.WithTransformation(SKMatrix.CreateScale(0, 0)));
            a.LayoutRoot();

            AssertRect(a.Layout, 0, 0, 300, 100);
        }

        [TestMethod]
        public void CalculateRootLayout_OnChild_DoesNothing()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                RenderOnce(b);

                b.LayoutRoot();

                Assert.IsNull(b.Bounds);
                Assert.AreEqual(0, b.Warnings.Count);
            }
        }

        [TestMethod]
        public void Attach_FormerRoot_DropsItsPlaceAndViewport()
        {
            using Node p = new("p"), a = new("a");
            RenderOnce(a, c => c.WithTransformation(SKMatrix.CreateTranslation(150, 50)));
            a.LayoutRoot();

            p.SetChildren(Spread.Create(a));
            AssertRect(a.Bounds, 0, 0, 300, 100);

            p.SetChildren(Spread<Node>.Empty);
            a.LayoutRoot();
            AssertRect(a.Bounds, 0, 0, 300, 100);
        }

        [TestMethod]
        public void SetBounds_Root_LaysOutIntoThemWithoutRender()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                root.Native->SetWidthAuto();
                root.Native->SetHeightAuto();

                root.SetBounds(new RectangleF(20, 30, 200, 50));
                root.LayoutRoot();

                AssertRect(root.Layout, 0, 0, 200, 50);
                AssertRect(root.Bounds, 20, 30, 200, 50);
                AssertRect(b.Bounds, 10, 10, 80, 30);
            }
        }

        [TestMethod]
        public void SetBounds_Root_PlacesRootAtTheirTopLeft()
        {
            using var a = new Node("a");
            var log = new List<(string Name, SKMatrix Caller, SKMatrix Canvas)>();
            a.SetLayer(new RecordingLayer("a", log));
            a.SetBounds(new RectangleF(20, 30, 200, 50));

            a.LayoutRoot();
            RenderOnce(a);

            Assert.AreEqual(1, log.Count);
            AssertTranslation(log[0].Caller, 20, 30);
        }

        [TestMethod]
        public void SetBounds_WinOverViewport()
        {
            using var a = new Node("a");
            RenderOnce(a);

            a.SetBounds(new RectangleF(0, 0, 200, 50));
            a.LayoutRoot();

            AssertRect(a.Layout, 0, 0, 200, 50);
        }

        [TestMethod]
        public void SetBounds_Null_FallsBackToViewportAndComputedBounds()
        {
            using var a = new Node("a");
            RenderOnce(a);
            a.SetBounds(new RectangleF(20, 30, 200, 50));
            a.LayoutRoot();

            a.SetBounds(null);
            a.LayoutRoot();

            AssertRect(a.Layout, 0, 0, 300, 100);
            AssertRect(a.Bounds, 0, 0, 300, 100);
        }

        [TestMethod]
        public void SetBounds_NotFinite_IsIgnoredWithWarning()
        {
            using var a = new Node("a");

            a.SetBounds(new RectangleF(0, 0, float.NaN, 50));

            Assert.IsNull(a.Bounds);
            Assert.AreEqual(1, a.Warnings.Count);
        }

        [TestMethod]
        public void SetBounds_NegativeSize_IsIgnoredWithWarning()
        {
            using var a = new Node("a");

            a.SetBounds(new RectangleF(0, 0, -10, 50));

            Assert.IsNull(a.Bounds);
            Assert.AreEqual(1, a.Warnings.Count);
        }

        [TestMethod]
        public void SetBounds_Child_IsReportedButDoesNotPlaceIt()
        {
            var (root, b, c) = CreateRow();
            using (root) using (b) using (c)
            {
                b.SetBounds(new RectangleF(1, 2, 3, 4));
                root.CalculateLayout();

                Assert.AreEqual(1, b.Warnings.Count);
                AssertRect(b.Layout, 10, 10, 80, 80);
                AssertRect(b.Bounds, 1, 2, 3, 4);
            }
        }

        [TestMethod]
        public void CalculateLayout_AfterArea_PlacesRootAtZero()
        {
            using var a = new Node("a");
            a.SetBounds(new RectangleF(20, 30, 200, 50));
            a.LayoutRoot();
            a.SetBounds(null);

            a.CalculateLayout(300, 100);

            AssertRect(a.Bounds, 0, 0, 300, 100);
        }
    }
}
