using VL.Core;
using VL.Core.Import;
using VL.Core.PublicAPI;
using VL.Fu.Core.Rendering;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Core.Region
{
    /// <summary>
    /// A node whose content is patched inside it, as a VL region. Continues the interface chain after <see cref="IRenderable"/>,
    /// since the inlay draws into the node's layer.
    /// </summary>
    public interface IRegionProvider : IRenderable, IRegion<IRegionProvider.IRegionInlay>
    {
        public interface IRegionInlay
        {
            void Update(IFuNode node, out ILayer layer);
        }
    }

    /// <summary>
    /// Closes the element chain at <see cref="IFuNode"/>: every node in a Fu tree derives from <see cref="RegionProvider"/>,
    /// and its parent and children are typed as <see cref="IFuNode"/>.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class RegionProvider : Renderable<IFuNode>, IFuNode
    {
        private IRegionProvider.IRegionInlay? _inlay;
        private readonly Dictionary<InputDescription, object?> _inputs = new();
        private readonly Dictionary<OutputDescription, object?> _outputs = new();

        protected RegionProvider([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetPatchInlayFactory(Func<IRegionProvider.IRegionInlay> patchInlayFactory)
        {
            if (_inlay is null)
                _inlay = patchInlayFactory();
        }

        [Fragment]
        public virtual void Update()
        {
            if (_inlay is null)
                return;

            _inlay.Update(this, out var layer);

            this.SetLayer(layer);
        }

        public void AcknowledgeInput(in InputDescription description, object? outerValue)
        {
            _inputs[description] = outerValue;
        }

        public void AcknowledgeOutput(
            in OutputDescription description,
            IRegionProvider.IRegionInlay patchInlay,
            object? innerValue
        )
        {
            _outputs[description] = innerValue;
        }

        public void RetrieveInput(
            in InputDescription description,
            IRegionProvider.IRegionInlay patchInlay,
            out object? innerValue
        )
        {
            _inputs.TryGetValue(description, out innerValue);
        }

        public void RetrieveOutput(in OutputDescription description, out object? outerValue)
        {
            _outputs.TryGetValue(description, out outerValue);
        }

        /// <summary>
        /// Disposes the patch inlay, which this node created and therefore owns, before leaving the tree.
        /// A patch compiles to <see cref="IDisposable"/> when it holds disposable state, like the child nodes patched inside.
        /// Control point values are only dropped: they belong to the patch around the region.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var inlay = _inlay;
                _inlay = null;
                (inlay as IDisposable)?.Dispose();

                _inputs.Clear();
                _outputs.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
