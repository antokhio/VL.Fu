using VL.Fu.Core.Common;

namespace VL.Fu.Core.Interaction
{
    public interface IFuGesture : IContextConsumer
    {
        /// <summary>
        /// The current state of the gesture's recognition process.
        /// </summary>
        GestureState State { get; } // ???
    }
}
