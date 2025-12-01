namespace VL.Fu.Core.Interaction
{
    public interface IFuBehaviour : IContextConsumer
    {
        IReadOnlyList<IFuGesture> Gestures { get; }
        bool Enabled { get; }
        int Priority { get; }
        bool IsTransient { get; }

        void OnStart(IFuNode host, FuGestureEvent ev);
        void OnUpdate(IFuNode host, FuGestureEvent ev);
        void OnFinish(IFuNode host, FuGestureEvent ev);
        void OnCancel(IFuNode host, FuGestureEvent ev);
    }
}
