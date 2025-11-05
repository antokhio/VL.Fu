namespace VL.Fu.Core.InstanceId
{
    public abstract class InstanceIdBase : IInstanceId
    {
        public int InstanceId { get; } = InstanceIdHelper.Next();
    }
}
