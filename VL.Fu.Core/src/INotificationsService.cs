using System.Reactive;
using VL.Fu.Core.Input;

namespace VL.Fu.Core
{
    /// <summary>
    /// Defines the contract for a service that processes raw user interaction notifications
    /// and exposes them as structured, reactive streams of input state.
    /// </summary>
    public interface INotificationsService : IContextedService, INotifiable, IDisposable
    {
        /// <summary>
        /// A stream that emits <c>true</c> when user input is globally enabled, and <c>false</c> otherwise.
        /// </summary>
        IObservable<bool> EnabledStream { get; }

        /// <summary>
        /// A stream indicating whether the UI context has focus. Emits <c>true</c> on focus gain and <c>false</c> on focus loss.
        /// </summary>
        IObservable<bool> FocusedStream { get; }

        /// <summary>
        /// An event stream that signals specifically when focus is gained.
        /// </summary>
        IObservable<Unit> OnFocusFound { get; }

        /// <summary>
        /// An event stream that signals specifically when focus is lost.
        /// </summary>
        IObservable<Unit> OnFocusLost { get; }

        /// <summary>
        /// An event stream that signals a reset event, typically when the context becomes disabled or loses focus.
        /// Used for clearing transient input states.
        /// </summary>
        IObservable<Unit> OnReset { get; }

        /// <summary>
        /// A stream providing the current state of the mouse, including position, button presses, and wheel data.
        /// </summary>
        IObservable<FuMouse> MouseStream { get; }

        /// <summary>
        /// A unified stream providing the current state of all active pointers, abstracting both touch and mouse inputs
        /// into a common data structure.
        /// </summary>
        IObservable<IReadOnlyDictionary<int, FuPointer>> PointersStream { get; }

        /// <summary>
        /// A stream providing the set of currently pressed keyboard keys.
        /// </summary>
        IObservable<IReadOnlySet<FuKey>> KeysStream { get; }

        /// <summary>
        /// A stream indicating whether a touch interaction (as opposed to mouse) is currently active.
        /// </summary>
        IObservable<bool> TouchActiveStream { get; }

        /// <summary>
        /// An event stream that signals the start of a touch interaction.
        /// </summary>
        IObservable<Unit> OnTouchActive { get; }

        /// <summary>
        /// A stream indicating whether there has been any recent user activity (mouse, keyboard, or touch).
        /// </summary>
        IObservable<bool> IsActiveStream { get; }

        /// <summary>
        /// The main, consolidated output stream. It combines all other input streams into a single,
        /// comprehensive <see cref="FuInputState"/> object representing the complete input context at any moment.
        /// </summary>
        IObservable<FuInputState> InputStateStream { get; }
    }
}
