using VL.Fu.Core.HitTest;
using VL.Fu.Core.InstanceId;
using VL.Skia;

namespace VL.Fu.Core
{
    /// <summary>
    /// Defines a contract for an object that can host interactive behaviors.
    /// It provides a stable identifier and the necessary components for hit-testing.
    /// </summary>
    public interface IInteractiveHost : IHitTestProvider, IRendering, IInstanceId { }
}
