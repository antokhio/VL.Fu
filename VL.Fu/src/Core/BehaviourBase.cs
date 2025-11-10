using VL.Core.Import;
using VL.Fu.Core.Behaviour;
using VL.Fu.Core.Common;
using VL.Fu.Core.Gesture;
using VL.Fu.Core.InstanceId;

namespace VL.Fu.Core
{
    /// <summary>
    /// An abstract base class for behaviors, providing an instance ID and a default priority.
    /// Concrete interactive behaviors should inherit from this and implement IInteractiveBehavior.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class BehaviourBase : RepositoryConsumer, IInstanceId
    {
        protected IInteractiveHost? Host { get; private set; }

        /// <summary>
        /// Priority higher is more imidiate
        /// </summary>
        public abstract int Priority { get; }

        /// <summary>
        /// Gets whether this behavior is transient. Defaults to false.
        /// Transient behaviors do not capture the pointer.
        /// </summary>
        public virtual bool IsTransient => false;

        public abstract IEnumerable<IGesture> Gestures { get; }

        public virtual void SetHost(IInteractiveHost host)
        {
            Host = host;
        }

        /// <summary>
        /// Sets the context ID for this behavior and propagates it to all owned gestures.
        /// </summary>
        public override void SetContextId(int contextId)
        {
            base.SetContextId(contextId);
            if (Gestures != null)
            {
                foreach (var gesture in Gestures)
                {
                    if (gesture is null)
                        continue;

                    gesture?.SetContextId(contextId);
                    gesture?.SetHost(Host);
                }
            }
        }

        [Fragment(Order = PinOrder.Output)]
        public IInteractiveBehavior Output => (IInteractiveBehavior)(object)this;
    }
}
