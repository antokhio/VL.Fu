using VL.Core;
using VL.Core.Import;
using VL.Core.PublicAPI;
using VL.Fu.Core;

namespace VL.Fu
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class FuRegion : Interactable, IRegion<IFuNodeInlay>
    {
        private readonly Dictionary<InputDescription, object?> _inputs = new();
        private readonly Dictionary<OutputDescription, object?> _outputs = new();

        private IFuNodeInlay? _inlay;

        private NodeContext _nodeContext;

        [Fragment]
        protected FuRegion(NodeContext nodeContext)
            : base(nodeContext)
        {
            _nodeContext = nodeContext;
        }

        [Fragment]
        public virtual void Update()
        {
            if (_inlay is null)
                return;

            _inlay.Evaluate((IFuNode)this, out var layer);

            this.SetLayer(layer);
        }

        public void SetPatchInlayFactory(Func<IFuNodeInlay> patchInlayFactory)
        {
            if (_inlay is null)
                _inlay = patchInlayFactory();
        }

        public void AcknowledgeInput(in InputDescription description, object? outerValue)
        {
            _inputs[description] = outerValue;
        }

        public void AcknowledgeOutput(
            in OutputDescription description,
            IFuNodeInlay patchInlay,
            object? innerValue
        )
        {
            _outputs[description] = innerValue;
        }

        public void RetrieveInput(
            in InputDescription description,
            IFuNodeInlay patchInlay,
            out object? innerValue
        )
        {
            _inputs.TryGetValue(description, out innerValue);
        }

        public void RetrieveOutput(in OutputDescription description, out object? outerValue)
        {
            _outputs.TryGetValue(description, out outerValue);
        }
    }
}
