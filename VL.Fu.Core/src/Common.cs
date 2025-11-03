namespace VL.Fu.Core
{
    public static class Common
    {
        public static int DefaultCallerHash = -1;

        public struct PinOrder
        {
            public const int None = 0;

            public const int Exclusive = -100;

            public const int Main = -50;

            public const int Secondary = -40;

            public const int Style = -30;

            public const int Action = -20;

            public const int Enabled = 50;
        }

        public struct BehaviourPriority
        {
            public const int Default = 0;

            public const int Low = 0;

            public const int Drag = 1;
        }
    }
}
