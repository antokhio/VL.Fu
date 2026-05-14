using System.Reactive.Disposables;
using System.Reactive.Linq;
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Extensions;
using VL.Fu.Core.Notfications;
using VL.Fu.Core.Property;
using VL.Lib.Mathematics;
using VL.Lib.Reactive;
using VL.Skia;

namespace VL.Fu.Core
{
    public abstract class ViewportBoundsProvider : Configuration, IRendering, IDisposable
    {
        protected readonly IChannel<SKRect> _viewportBounds = new ChannelProperty<SKRect>(
            SKRect.Empty
        );
        protected readonly IChannel<SKMatrix> _viewportTransformation =
            new ChannelProperty<SKMatrix>(SKMatrix.Identity);

        protected readonly IChannel<float> _viewportScaling = new ChannelProperty<float>(
            Constants.DefaultScaling
        );

        private readonly CompositeDisposable _subscriptions = new();

        protected ViewportBoundsProvider(NodeContext nodeContext)
            : base(nodeContext)
        {
            var viewportStream = Observable
                .CombineLatest(
                    _viewportBounds.StartWith(SKRect.Empty),
                    _viewportTransformation.StartWith(SKMatrix.Identity),
                    ScalingMode.StartWith(Constants.DefaultScalingMode),
                    Space.StartWith(Constants.DefaultSpace),
                    _viewportScaling.StartWith(Constants.DefaultScaling),
                    (bounds, transformation, scalingMode, currentSpace, scaling) =>
                    {
                        var top = bounds.Top;
                        var left = bounds.Left;
                        var right = bounds.Right;
                        var bottom = bounds.Bottom;

                        if (transformation.TryInvert(out var inverse))
                        {
                            var topLeft = Conversions.AsVector2(inverse.MapPoint(left, top));
                            var bottomRight = Conversions.AsVector2(
                                inverse.MapPoint(right, bottom)
                            );

                            RectangleNodes.JoinPoints(ref topLeft, ref bottomRight, out var rect);

                            return new
                            {
                                Bounds = rect.WithScalingMode(currentSpace, scalingMode, scaling),
                                Scaling = scaling,
                            };
                        }

                        return new { Bounds = RectangleF.Empty, Scaling = scaling };
                    }
                )
                .DistinctUntilChanged();

            _subscriptions.Add(
                viewportStream.Subscribe(state =>
                {
                    Bounds = state.Bounds;

                    BroadcastNotification(
                        new ViewportBoundsNotification(state.Bounds, state.Scaling, this)
                    );
                })
            );
        }

        public RectangleF? Bounds { get; private set; }

        public virtual void Render(CallerInfo caller)
        {
            _viewportBounds.EnsureValue(caller.ViewportBounds);
            _viewportTransformation.EnsureValue(caller.Transformation);
            _viewportScaling.EnsureValue(caller.Scaling);
        }

        public override void Dispose()
        {
            _subscriptions.Dispose();

            base.Dispose();
        }
    }
}
