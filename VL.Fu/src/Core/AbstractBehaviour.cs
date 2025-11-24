using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Behaviours;
using VL.Fu.Core.Common;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Property;

namespace VL.Fu.Core
{
    // Proptype behaviour base node
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class AbstractBehaviour : ContextConsumer, IFuBehaviour
    {
        public abstract int Priority { get; }
        public abstract bool IsTransient { get; }
        public bool Enabled => _enabled.Value;
        public abstract IReadOnlyList<IFuGesture> Gestures { get; }

        private readonly CachedProperty<bool> _enabled = new(true);

        [Fragment]
        protected AbstractBehaviour(NodeContext nodeContext)
            : base(nodeContext) { }

        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled) => _enabled.TrySetValue(enabled);
    }
}
