using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Core.Behaviours
{
    public interface IFuBehaviour : IContextConsumer
    {
        /// <summary>
        /// The priority of the behavior, used for sorting. Higher numbers are processed first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Indicates whether the behavior is transient. Transient behaviors (like hover) do not capture
        /// pointers but are notified of pointer movements over their host.
        /// </summary>
        bool IsTransient { get; }

        /// <summary>
        /// Gets whether the behavior is currently enabled and should process input.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// A collection of gestures that can activate this behavior.
        /// </summary>
        IReadOnlyList<IFuGesture> Gestures { get; }

        bool TryActivate(IFuNode host, FuInputState inputState, out object? actovator);
    }
}
