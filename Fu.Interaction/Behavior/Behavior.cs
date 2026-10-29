using VL.Lib.IO.Notifications;
using VL.Skia;

namespace Fu.Interaction.Behavior
{
    public interface IInteractionBehavior : IBehavior
    {
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Serves as the base contract for modular interaction logic, encapsulating stateful
    /// gesture recognition and input processing decoupled from specific UI elements.
    /// </summary>
    public abstract class Behavior : IInteractionBehavior
    {
        /// <summary>
        /// Value indicating whether this behavior actively listens to and processes notifications.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Processes an incoming notification and determines whether the interaction was consumed.
        /// </summary>
        public abstract bool Notify(INotification notification, CallerInfo caller);
    }
}
