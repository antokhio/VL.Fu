using VL.Fu.Core.Input;
using VL.Fu.Core.InstanceId;

namespace VL.Fu.Core.Interaction
{
    public interface IFuGesture : IInstanceId
    {
        GestureStatus Status { get; }
        int Priority { get; }
        IFuBehaviour Behaviour { get; }
        IFuNode? Host { get; set; }
        IReadOnlyList<FuPointer> Activators { get; }

        /// <summary>
        /// Updates the internal Status.
        /// </summary>
        /// <param name="inputState">Global input state.</param>
        /// <param name="candidates">
        /// A list of pointers that have already passed the HitTest for this gesture's Host.
        /// Ordered by exclusivity (pointers hitting fewer objects first).
        /// </param>
        void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates);

        void Reset();
    }
}
