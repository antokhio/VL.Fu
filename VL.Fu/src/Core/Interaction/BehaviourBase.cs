using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Common;
using VL.Fu.Core.Property;

namespace VL.Fu.Core.Interaction
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class BehaviourBase : ContextConsumer, IFuBehaviour
    {
        public IReadOnlyList<IFuGesture> Gestures { get; protected set; } =
            Array.Empty<IFuGesture>();
        public virtual int Priority => _priority.Value;
        public virtual bool Enabled => _enabled.Value;
        public virtual bool IsTransient => false;

        protected readonly CachedProperty<bool> _enabled = new(true);
        protected readonly CachedProperty<int> _priority = new(GesturePriority.None);

        [Fragment]
        protected BehaviourBase(NodeContext nodeContext)
            : base(nodeContext) { }

        public virtual void OnStart(IFuNode host, FuGestureEvent ev) { }

        public virtual void OnUpdate(IFuNode host, FuGestureEvent ev) { }

        public virtual void OnFinish(IFuNode host, FuGestureEvent ev) { }

        public virtual void OnCancel(IFuNode host, FuGestureEvent ev) { }

        [Fragment(Order = PinOrder.Priority)]
        public virtual void SetPriority(int priority = GesturePriority.None)
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
