using VL.Core;
using VL.Core.Import;
using VL.Core.PublicAPI;
using VL.Fu.Core.Context;

namespace VL.Fu.Core.Root
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    [Region(SupportedBorderControlPoints = ControlPointType.None)]
    public abstract class ContextProvider : ServiceRegistry, IContextProvider
    {
        public IFuNode? Root { get; protected set; }

        protected readonly Dictionary<InputDescription, object?> _inputs = new();
        protected readonly Dictionary<OutputDescription, object?> _outputs = new();

        protected readonly ContextProviderStore _store = new();
        protected readonly IReadOnlyList<object> _storeValues;

        private IContextProviderInlay? _inlay;
        protected NodeContext _nodeContext;

        [Fragment]
        public ContextProvider(NodeContext nodeContext)
            : base()
        {
            _nodeContext = nodeContext;
            _storeValues = [this];

            ConfigureStore(ContextProviderHelper.RootContextProviderName);
        }

        protected void ConfigureStore(string contextName)
        {
            IReadOnlyList<BorderControlPointDescription> storeInputs =
            [
                new BorderControlPointDescription(
                    contextName,
                    typeof(IContextProvider),
                    InstanceId,
                    false
                ),
            ];

            _store.Configurate(storeInputs);
        }

        [Fragment]
        public virtual void Update()
        {
            if (_inlay is null)
                return;

            using (var scope = _store.ActivateScope(_nodeContext, _storeValues))
            {
                // Execute user's patched logic while scope is active
                _inlay.Execute(this, out var root);

                Root = root;
                // Scope automatically disposed here, popping the layer
            }
        }

        public void SetPatchInlayFactory(Func<IContextProviderInlay> patchInlayFactory)
        {
            if (_inlay is null)
                _inlay = patchInlayFactory();
        }

        public void AcknowledgeInput(in InputDescription cp, object? outerValue)
        {
            _inputs[cp] = outerValue;
        }

        public void AcknowledgeOutput(
            in OutputDescription cp,
            IContextProviderInlay patchInstance,
            object? innerValue
        )
        {
            _outputs[cp] = innerValue;
        }

        public void RetrieveInput(
            in InputDescription cp,
            IContextProviderInlay patchInstance,
            out object? innerValue
        )
        {
            _inputs.TryGetValue(cp, out innerValue);
        }

        public void RetrieveOutput(in OutputDescription cp, out object? outerValue)
        {
            _outputs.TryGetValue(cp, out outerValue);
        }

        public override void Dispose()
        {
            if (_inlay is IDisposable disposableInlay)
            {
                disposableInlay.Dispose();
            }
            _inlay = null;

            Root = null;

            if (_store is IDisposable disposableStore)
            {
                disposableStore.Dispose();
            }

            _inputs.Clear();
            _outputs.Clear();

            base.Dispose();
        }
    }
}
