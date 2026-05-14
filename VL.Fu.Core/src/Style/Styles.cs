using YogaSharp;

namespace VL.Fu.Core
{
    public static class Styles
    {
        public static FuStyle SetDirection(
            FuStyle? style,
            YGDirection direction = YGDirection.Inherit
        ) =>
            new(
                style,
                node =>
                {
                    unsafe
                    {
                        node.Handle->SetDirection(direction);
                    }
                }
            );

        public static FuStyle SetWidth(FuStyle? style, float width = 0.0f) =>
            new(
                style,
                node =>
                {
                    unsafe
                    {
                        node.Handle->SetWidth(width);
                    }
                }
            );

        public static FuStyle SetHeight(FuStyle? style, float height = 0.0f) =>
            new(
                style,
                node =>
                {
                    unsafe
                    {
                        node.Handle->SetHeight(height);
                    }
                }
            );

        public static FuStyle SetPositionY(FuStyle? style, float positionY = 0.0f) =>
            new(
                style,
                node =>
                {
                    unsafe
                    {
                        node.Handle->SetPosition(YGEdge.Top, positionY);
                    }
                }
            );
    }
}
