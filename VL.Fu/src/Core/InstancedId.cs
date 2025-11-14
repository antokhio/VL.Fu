using VL.Fu.Core.InstanceId;

namespace VL.Fu.Core
{
    public abstract class InstancedId : IInstanceId
    {
        public int InstanceId { get; } = InstanceIdHelper.Next();
    }
}
