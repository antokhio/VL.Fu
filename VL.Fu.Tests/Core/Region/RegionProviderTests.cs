using VL.Fu.Core.Region;
using VL.Skia;

namespace VL.Fu.Tests.Core.Region
{
    [TestClass]
    public sealed class RegionProviderTests
    {
        private class Inlay : IRegionProvider.IRegionInlay
        {
            public int Updates { get; private set; }

            public void Update(IFuNode node, out ILayer layer)
            {
                Updates++;
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
    }
}
