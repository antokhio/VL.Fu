using VL.Fu.Core.Interaction;

namespace VL.Fu.Core
{
    public interface IInteractionService : IContextedService
    {
        IObservable<IReadOnlyList<IFuGesture>> GestureDispatch { get; }
    }
}
