using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Core.Helpers;
using VL.Fu.Extensions;
using VL.Skia;
using VL.UI.Core;

namespace VL.Fu.Services
{
    public class ViewportService
    {
        public int DIPFactor => _instance.DIPFactor;
        public int PixelFactor => _instance.PixelFactor;
        public CommonSpace Space => _instance.Space;
        public RectangleF Bounds { get; private set; } = RectangleF.Empty;
        public Int2 Resoultion { get; private set; } = Int2.One;
        public float DIP => DIPHelpers.DIPFactor();

        private readonly Fu _instance;
        private readonly CachedProperty<SKRect?> _bounds = new(null);

        public ViewportService(Fu instance)
        {
            _instance = instance;
            _instance.OnUpdateSpace += (space) => UpdateBounds(_bounds.Value);
        }

        public void OnRender(CallerInfo caller)
        {
            _bounds.TrySetValue(caller.ViewportBounds, (prev, next) => UpdateBounds(next));
        }

        public void UpdateBounds(SKRect? rect)
        {
            if (rect is not null)
            {
                SKRect bounds = rect.Value;

                Resoultion = new Int2((int)bounds.Width, (int)bounds.Height);

                var boundsF = Conversions.ToRectangleF(ref bounds);

                switch (Space)
                {
                    case CommonSpace.Normalized:
                        Bounds = boundsF.ToNormalizedSpace(Resoultion);
                        break;
                    case CommonSpace.DIP:
                        Bounds = boundsF.ToCenteredDIPSpace(Resoultion, DIPFactor);
                        break;
                    case CommonSpace.DIPTopLeft:
                        Bounds = boundsF.ToDIPTopLeftSpace(DIPFactor);
                        break;
                    case CommonSpace.PixelTopLeft:
                        // Seems to be a bug in gamma
                        Bounds = boundsF.ToDIPTopLeftSpace(PixelFactor);
                        break;
                }
            }
        }
    }
}
