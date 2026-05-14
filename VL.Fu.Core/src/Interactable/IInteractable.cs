using VL.Fu.Core.HitTest;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Core
{
    public interface IInteractable : IAreaTestProvider, IHitTestProvider, IInstanceId
    {
        /// <summary>
        /// Gets the list of behaviors attached to this interactable node.
        /// </summary>
        IReadOnlyList<IFuBehaviour> Behaviours { get; }
    }
}
