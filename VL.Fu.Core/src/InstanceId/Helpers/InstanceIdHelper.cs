namespace VL.Fu.Core.InstanceId.Helpers
{
    internal static class InstanceIdHelper
    {
        private static int _lastId = 0;

        public static int Next() => System.Threading.Interlocked.Increment(ref _lastId);
    }
}
