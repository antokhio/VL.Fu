using VL.Fu.Core.Gesture;
using VL.Fu.Core.Repository;

namespace VL.Fu.Core.Behaviour
{
    /// <summary>
    /// Defines the contract for a behavior that can participate in the high-level interaction system.
    /// It includes a structured lifecycle, gesture declaration, priority, and is a repository consumer.
    /// </summary>
    public interface IInteractiveBehavior : IRepositoryConsumer
    {
        /// <summary>
        /// The priority of the behavior, used for sorting. Higher numbers are processed first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// If true, this behavior will not capture the pointer or stop the search for other behaviors
        /// when its gesture matches. Useful for passive states like hovering.
        /// </summary>
        bool IsTransient { get; }

        /// <summary>
        /// Called by the host to provide the behavior with a reference to itself.
        /// This allows the behavior to query properties from its host (e.g., Bounds).
        /// </summary>
        /// <param name="host">The interactive host that owns this behavior.</param>
        void SetHost(IInteractiveHost host);

        /// <summary>
        /// A collection of gestures that can activate this behavior.
        /// </summary>
        IEnumerable<IGesture> Gestures { get; }

        /// <summary>
        /// Called by the InteractionService when one of the behavior's gestures is matched.
        /// </summary>
        /// <param name="gesture">The gesture that was successfully matched.</param>
        void OnActivate(IGesture gesture);

        /// <summary>
        /// Called when an ongoing interaction is updated (e.g., pointer move).
        /// </summary>
        /// <param name="context">The current input context.</param>
        void OnAdvance(GestureInputContext context);

        /// <summary>
        /// Called when the interaction ends (e.g., pointer up).
        /// </summary>
        void OnDeactivate(GestureInputContext context);

        /// <summary>
        /// Called when the interaction is interrupted by a higher-priority behavior.
        /// </summary>
        void OnCancel();
    }
}
