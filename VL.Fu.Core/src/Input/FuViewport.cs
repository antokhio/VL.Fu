using Stride.Core.Mathematics;
using VL.Fu.Core.Common;
using VL.Fu.Core.Extensions;
using VL.Skia;

namespace VL.Fu.Core.Input
{
    public record struct FuViewport
    {
        public int PixelFactor { get; init; } = Constants.DefaultPixelFactor;
        public float InversePixelFactor => 1.0f / PixelFactor;
        public int DIPFactor { get; init; } = Constants.DefaultDIPFactor;
        public float InverseDIPFactor => 1.0f / DIPFactor;
        public ScalingMode ScalingMode { get; init; } = Constants.DefaultScalingMode;
        public float Scaling { get; init; } = Constants.DefaultScaling;
        public Int2 Resolution { get; init; } = Int2.Zero;
        public Vector2 ClientArea { get; init; } = Vector2.Zero;
        public CommonSpace Space { get; init; } = Constants.DefaultSpace;
        public RectangleF ViewportBounds { get; init; } = RectangleF.Empty;

        public FuViewport(
            int pixelFactor,
            int dipFactor,
            ScalingMode scalingMode,
            float scaling,
            Vector2 clientArea,
            CommonSpace space,
            RectangleF bounds
        )
        {
            ScalingMode = scalingMode;
            Scaling = scaling;
            ClientArea = clientArea;
            Resolution = clientArea.ToInt();

            PixelFactor = pixelFactor.WithPixelFactorScalingMode(ScalingMode, Scaling);
            DIPFactor = dipFactor.WithDIPFactorScalingMode(ScalingMode, Scaling);

            Space = space;
            ViewportBounds = bounds;
        }
    }
}
