using Stride.Core.Mathematics;
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
            /// <summary>
            /// Runs the patch inside the node. <paramref name="bounds"/> is the node's box in its own space, where the layer draws:
            /// (0, 0) is its top-left corner and the size is the one from <see cref="Core.Layout.ILayoutable.Layout"/>.
            /// </summary>
            /// <remarks>
            /// The inlay runs before the root lays the tree out, so the size is the one from the previous layout pass,
            /// and it is empty until the node was laid out once.
            /// </remarks>
            void Update(IFuNode node, RectangleF bounds, out ILayer layer);
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

            var layout = Layout;
            _inlay.Update(this, new RectangleF(0, 0, layout.Width, layout.Height), out var layer);

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
