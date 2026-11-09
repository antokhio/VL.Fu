using Stride.Core.Mathematics;
using VL.Fu.Core.Region;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Lib.Collections;
using VL.Skia;
using YogaSharp;

namespace VL.Fu.Tests.Core.Region
{
    [TestClass]
    public sealed class RegionProviderTests
    {
        private class Inlay : IRegionProvider.IRegionInlay
        {
            public int Updates { get; private set; }

            public RectangleF LastBounds { get; private set; }

            public void Update(IFuNode node, RectangleF bounds, out ILayer layer)
            {
                Updates++;
                LastBounds = bounds;
                layer = null!;
            }
        }

        private sealed class DisposableInlay : Inlay, IDisposable
        {
            public int Disposals { get; private set; }

            public void Dispose() => Disposals++;
        }

        [TestMethod]
        public void Dispose_DisposesInlayOnce()
        {
            var inlay = new DisposableInlay();
            var node = new FuNode(null!);
            node.SetPatchInlayFactory(() => inlay);

            node.Dispose();
            node.Dispose();

            Assert.AreEqual(1, inlay.Disposals);
        }

        [TestMethod]
        public void Dispose_NonDisposableInlay_DoesNotThrow()
        {
            var node = new FuNode(null!);
            node.SetPatchInlayFactory(() => new Inlay());

            node.Dispose();
        }

        [TestMethod]
        public void Update_AfterDispose_DoesNotRunInlay()
        {
            var inlay = new DisposableInlay();
            var node = new FuNode(null!);
            node.SetPatchInlayFactory(() => inlay);
            node.Update();

            node.Dispose();
            node.Update();

            Assert.AreEqual(1, inlay.Updates);
        }

        [TestMethod]
        public void FuNodeUpdate_Root_LaysOutIntoBounds()
        {
            using var node = new FuNode(null!);
            node.SetBounds(new RectangleF(20, 30, 200, 50));

            node.Update();

            Assert.AreEqual(new RectangleF(0, 0, 200, 50), node.Layout);
        }

        [TestMethod]
        public void FuNodeUpdate_Child_DoesNotLayOut()
        {
            using FuNode root = new(null!), child = new(null!);
            root.SetChildren(Spread.Create<IFuNode>(child));
            child.SetBounds(new RectangleF(0, 0, 200, 50));

            child.Update();

            Assert.AreEqual(RectangleF.Empty, child.Layout);
        }

        [TestMethod]
        public void Update_InlayBounds_AreEmptyBeforeLayout()
        {
            var inlay = new Inlay();
            using var node = new FuNode(null!);
            node.SetPatchInlayFactory(() => inlay);
            node.SetBounds(new RectangleF(20, 30, 200, 50));

            node.Update();

            Assert.AreEqual(RectangleF.Empty, inlay.LastBounds);
        }

        [TestMethod]
        public void Update_InlayBounds_AreInNodeSpace()
        {
            var inlay = new Inlay();
            using FuNode root = new(null!), first = new(null!), second = new(null!);
            second.SetPatchInlayFactory(() => inlay);
            first.SetStyle(new HeightStyle(Array.Empty<IStyle>(), YGValue.Point(50)));
            second.SetStyle(new HeightStyle(Array.Empty<IStyle>(), YGValue.Point(50)));
            root.SetStyle(new FlexDirectionStyle(Array.Empty<IStyle>(), YGFlexDirection.Column));
            root.SetChildren(Spread.Create<IFuNode>(first, second));
            root.SetBounds(new RectangleF(20, 30, 200, 100));
            root.Update();

            second.Update();

            Assert.AreEqual(new RectangleF(0, 50, 200, 50), second.Layout);
            Assert.AreEqual(new RectangleF(0, 0, 200, 50), inlay.LastBounds);
        }
    }
}
