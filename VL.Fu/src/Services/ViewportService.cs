using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Extensions;
using VL.Skia;
using VL.UI.Core;

namespace VL.Fu.Services
{
    public class ViewportService
    {
        public CommonSpace Space { get; set; } = CommonSpace.Normalized;

        private static float ScaleFactor = 0.01f;
        public RectangleF Bounds { get; private set; } = RectangleF.Empty;
        public Int2 Resoultion { get; private set; } = Int2.One;
        public float DIP => DIPHelpers.DIPFactor();

        private SKRect? _bounds = null;

        public void OnRender(CallerInfo caller)
        {
            if (caller.ViewportBounds != _bounds)
            {
                var bounds = caller.ViewportBounds;
                _bounds = bounds;

                Resoultion = new Int2((int)bounds.Width, (int)bounds.Height);

                var boundsF = Conversions.ToRectangleF(ref bounds);

                switch (Space)
                {
                    case CommonSpace.Normalized:
                        Bounds = boundsF.ToNormalizedSpace(Resoultion);
                        break;
                    case CommonSpace.DIP:
                        boundsF = DIPHelpers.DIP(boundsF);

                        boundsF = new RectangleF(
                            boundsF.X * ScaleFactor,
                            boundsF.Y * ScaleFactor,
                            boundsF.Width * ScaleFactor,
                            boundsF.Height * ScaleFactor
                        );

                        Bounds = boundsF;
                        break;
                }
            }
        }
    }
}
