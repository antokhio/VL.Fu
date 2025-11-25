namespace VL.Fu.Core.Interaction
{
    public interface IFuBehaviour : IContextConsumer
    {
        IReadOnlyList<IFuGesture> Gestures { get; }
        int Priority { get; }
        bool IsTransient { get; }
        bool Enabled { get; }

        /// <summary>
        /// Called when one of the behavior's gestures transitions to a Matched state.
        /// </summary>
        void OnActivate(IFuNode host, FuGestureEvent ev);

        /// <summary>
        /// Called on every frame where the driving gesture continues to be valid/active.
        /// </summary>
        void OnAdvance(IFuNode host, FuGestureEvent ev);

        /// <summary>
        /// Called when the gesture ends successfully or the interaction completes.
        /// </summary>
        void OnDeactivate(IFuNode host, FuGestureEvent ev);

        /// <summary>
        /// Called when the interaction is forcefully cancelled.
        /// </summary>
        void OnCancel(IFuNode host);
    }
}
