using VL.Fu.Core.InstanceId.Helpers;

namespace VL.Fu.Core.InstanceId
{
    public abstract class InstanceIdBase : IInstanceId
    {
        public int InstanceId { get; } = InstanceIdHelper.Next();
    }
}
