using VL.Core.Import;
using VL.Fu.Core.Common;

namespace VL.Fu.Core
{
    /// <summary>
    /// An abstract base class for process nodes that are their own output.
    /// It provides a single output pin that exposes the node instance cast to type T.
    /// The inheriting class must be of type T.
    /// </summary>
    /// <typeparam name="T">The type of the output, which must match the inheriting class type.</typeparam>
    [ProcessNode]
    public abstract class OutputBase<T>
        where T : class
    {
        /// <summary>
        /// Exposes this node instance as an output of type T.
        /// </summary>
        [Fragment(Order = PinOrder.Output)]
        public T Output => (T)(object)this;
    }
}
