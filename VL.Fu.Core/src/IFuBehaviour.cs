using VL.Fu.Core.Repository;

namespace VL.Fu.Core
{
    public interface IFuBehaviour : IRepositoryConsumer
    {
        /// <summary>
        /// The priority of the behavior, used for sorting. Higher numbers are processed first.
        /// </summary>
        int Priority { get; }
    }
}
