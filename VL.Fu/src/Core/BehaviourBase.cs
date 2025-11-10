using VL.Fu.Core.InstanceId;

namespace VL.Fu.Core
{
    /// <summary>
    /// An abstract base class for behaviors, providing an instance ID and a default priority.
    /// Concrete interactive behaviors should inherit from this and implement IInteractiveBehavior.
    /// </summary>
    public abstract class BehaviourBase : RepositoryConsumer, IInstanceId
    {
        public abstract int Priority { get; }
    }
}
