using SkiaSharp;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Skia;

namespace VL.Fu
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class FuPrecompose : LinkedLayerBase, ILayer
    {
        private IFuNode? _input;
        private SKPaint _paint = new SKPaint();
        private SKColorF _color = new SKColorF(1, 1, 1, 1);

        [Fragment]
        public FuPrecompose() { }

        [Fragment]
        public void Update(IFuNode input, float alpha, out IFuNode output)
        {
            _input = input;

            _color = new SKColorF(_color.Red, _color.Green, _color.Blue, alpha);

            _paint.ColorF = _color;

            output = input;
        }

        public override void Render(CallerInfo caller)
        {
            if (_paint != null)
            {
                caller.Canvas.SaveLayer(_paint);
            }
            else
            {
                caller.Canvas.Save();
            }

            try
            {
                // Draw self
                _input?.Render(caller);

                // Draw hierarchy
                foreach (var child in _input?.Children ?? [])
                {
                    if (child is ILayer layer)
                    {
                        layer.Render(caller);
                    }
                }
            }
            finally
            {
                // 3. ALWAYS restore the canvas state, even if an exception occurs
                caller.Canvas.Restore();
            }
        }
    }
}
