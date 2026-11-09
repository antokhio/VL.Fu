using Fu.Core.Rendering;
using VL.Core;
using VL.Core.Import;
using VL.Core.PublicAPI;
using VL.Model;
using VL.Skia;

namespace Fu.Core.Region
{
    public interface IRegionProvider : IRegion<IRegionProvider.IRegionInlay>
    {
        public interface IRegionInlay
        {
            // TODO: properly define node
            void Update(in Renderable node, out ILayer layer);
        }
    }

    [ProcessNode]
    public abstract class RegionProvider : Renderable, IRegionProvider
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

        // todo Dispose
    }
}
