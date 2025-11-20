using VL.Fu.Core.Input;

namespace VL.Fu.Core
{
    /// <summary>
    /// Defines the contract for a service that processes and provides a unified stream of user input events.
    /// </summary>
    public interface IInputService : IContextedService, INotifiable
    {
        /// <summary>
        /// The main output stream that combines all input sources into a single, comprehensive state object.
        /// </summary>
        IObservable<FuInputState> InputStateStream { get; }

        /// <summary>
        /// A stream that indicates whether a touch interaction is currently active.
        /// </summary>
        IObservable<bool> IsTouchActiveStream { get; }

        /// <summary>
        /// A stream providing the current state of all active pointers (from touch and mouse).
        /// </summary>
        IObservable<IReadOnlyDictionary<int, FuPointer>> PointersStream { get; }

        /// <summary>
        /// A stream providing the current state of the mouse.
        /// </summary>
        IObservable<FuMouse> MouseStream { get; }

        /// <summary>
        /// A stream providing the set of currently pressed keys.
        /// </summary>
        IObservable<IReadOnlySet<FuKey>> KeysStream { get; }
    }
}
