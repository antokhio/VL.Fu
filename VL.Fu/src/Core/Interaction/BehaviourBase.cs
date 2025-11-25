using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;

namespace VL.Fu.Core.Interaction
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class BehaviourBase : ContextConsumer
    {
        public virtual int Priority => _priority.Value;
        public virtual bool Enabled => _enabled.Value;

        protected readonly CachedProperty<bool> _enabled = new(true);
        protected readonly CachedProperty<int> _priority = new(BehaviourPriority.None);

        [Fragment]
        protected BehaviourBase(NodeContext nodeContext)
            : base(nodeContext) { }

        [Fragment(Order = PinOrder.Priority)]
        public virtual void SetPriority(int priority = BehaviourPriority.None)
        {
            _priority.TrySetValue(priority);
        }

        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            _enabled.TrySetValue(enabled);
        }

        [Fragment(Order = PinOrder.Output)]
        public IFuBehaviour Output => (IFuBehaviour)this;
    }
}
