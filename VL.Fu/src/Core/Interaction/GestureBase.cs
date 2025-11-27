using VL.Fu.Core.Input;

namespace VL.Fu.Core.Interaction
{
    public abstract class GestureBase : InstancedId, IFuGesture
    {
        public IFuBehaviour Behaviour { get; }
        public IFuNode? Host { get; set; }

        public GestureStatus Status { get; protected set; } = GestureStatus.Idle;
        public virtual int Priority { get; protected set; } = 0;

        protected readonly List<FuPointer> _activators = new();
        public IReadOnlyList<FuPointer> Activators => _activators;

        protected GestureBase(IFuBehaviour behaviour)
        {
            Behaviour = behaviour;
        }

        public abstract void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates);

        public virtual void Reset()
        {
            Status = GestureStatus.Idle;
            _activators.Clear();
        }
    }
}
