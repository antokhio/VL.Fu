using Stride.Core.Mathematics;
using VL.Skia;

namespace VL.Fu.Extensions
{
    public static partial class SpaceExtensions
    {
        public static RectangleF FromResolution(
            this CommonSpace space,
            Int2 resolution,
            int dipFactor,
            int pixelFactor
        )
        {
            return space switch
            {
                CommonSpace.Normalized => resolution.ToNormalizedSpaceFromResolution(),
                CommonSpace.DIP => resolution.ToCenteredDIPSpaceFromResolution(dipFactor),
                CommonSpace.DIPTopLeft => resolution.ToDIPTopLeftSpaceFromResolution(dipFactor),
                CommonSpace.PixelTopLeft => resolution.ToPixelTopLeftSpaceFromResolution(
                    pixelFactor
                ),
                _ => throw new ArgumentOutOfRangeException(nameof(space), space, null),
            };
        }

        public static RectangleF ToNormalizedSpaceFromResolution(this Int2 resolution)
        {
            var height = 2.0f;
            var width = (resolution.X / (float)resolution.Y) * height;

            var positionX = width / 2.0f * -1.0f;
            var positionY = height / 2.0f * -1.0f;

            var rect = new RectangleF(0, 0, width, height);
            rect.Offset(positionX, positionY);

            return rect;
        }

        public static RectangleF ToCenteredDIPSpaceFromResolution(
            this Int2 resolution,
            int dipFactor
        )
        {
            var width = resolution.X / (float)dipFactor;
            var height = resolution.Y / (float)dipFactor;

            var positionX = width / 2.0f * -1.0f;
            var positionY = height / 2.0f * -1.0f;

            var rect = new RectangleF(0, 0, width, height);

            rect.Offset(positionX, positionY);
            return rect;
        }

        public static RectangleF ToDIPTopLeftSpaceFromResolution(
            this Int2 resolution,
            int dipFactor
        )
        {
            var width = resolution.X / (float)dipFactor;
            var height = resolution.Y / (float)dipFactor;
            return new RectangleF(0, 0, width, height);
        }

        public static RectangleF ToPixelTopLeftSpaceFromResolution(
            this Int2 resolution,
            int pixelFactor
        )
        {
            var width = resolution.X / (float)pixelFactor;
            var height = resolution.Y / (float)pixelFactor;
            return new RectangleF(0, 0, width, height);
        }
    }
}
