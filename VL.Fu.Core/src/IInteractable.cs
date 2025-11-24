using VL.Fu.Core.Behaviours;
using VL.Fu.Core.HitTest;
using VL.Fu.Core.Input;
using VL.Fu.Core.InstanceId;

namespace VL.Fu.Core
{
    public interface IInteractable : IInstanceId
    {
        /// <summary>
        /// Gets the list of behaviors attached to this interactable node.
        /// </summary>
        IReadOnlyList<IFuBehaviour> Behaviours { get; }

        /// <summary>
        /// Determines whether the specified pointer is currently over the object.
        /// </summary>
        /// <param name="pointer">The pointer to test against.</param>
        /// <returns>True if the pointer is over the object; otherwise, false.</returns>
        bool HitTest(FuPointer pointer);

        /// <summary>
        /// Executes the area test using the configured strategy.
        /// </summary>
        /// <param name="shape">Selection shape</param>
        bool IsContainedIn(ISelectionShape shape);
    }
}
