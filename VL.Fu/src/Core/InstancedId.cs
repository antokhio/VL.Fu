using VL.Fu.Core.InstanceId;

namespace VL.Fu.Core
{
    public abstract class InstancedId : IInstanceId
    {
        private static int _lastId = 0;
        public int InstanceId { get; } = System.Threading.Interlocked.Increment(ref _lastId);
    }
}
