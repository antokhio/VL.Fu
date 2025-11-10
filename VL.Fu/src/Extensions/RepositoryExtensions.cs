using Stride.Core.Mathematics;
using VL.Fu.Core.Repository;
using VL.Fu.Services;
using VL.Skia;

namespace VL.Fu.Extensions
{
    public static class RepositoryExtensions
    {
        public static Vector2 ToCurrentSpace(
            this IRepositoryConsumer consumer,
            Vector2 value,
            CommonSpace fromSpace
        )
        {
            var service = ServiceRepository.Instance.GetService<ViewportService>(
                consumer.ContextId
            );

            if (service is ViewportService viewportService)
            {
                return value.ConvertSpace(
                    fromSpace,
                    viewportService.Space,
                    viewportService.Resolution,
                    viewportService.DIPFactor,
                    viewportService.PixelFactor
                );
            }

            return value;
        }

        public static RectangleF ToCurrentSpace(
            this IRepositoryConsumer consumer,
            RectangleF value,
            CommonSpace fromSpace
        )
        {
            var service = ServiceRepository.Instance.GetService<ViewportService>(
                consumer.ContextId
            );

            if (service is ViewportService viewportService)
            {
                return value.ConvertSpace(
                    fromSpace,
                    viewportService.Space,
                    viewportService.Resolution,
                    viewportService.DIPFactor,
                    viewportService.PixelFactor
                );
            }

            return value;
        }

        public static float ToCurrentSpace(
            this IRepositoryConsumer consumer,
            float value,
            CommonSpace fromSpace
        )
        {
            var service = ServiceRepository.Instance.GetService<ViewportService>(
                consumer.ContextId
            );

            if (service is ViewportService viewportService)
            {
                return value.ConvertSpace(
                    fromSpace,
                    viewportService.Space,
                    viewportService.Resolution,
                    viewportService.DIPFactor,
                    viewportService.PixelFactor
                );
            }

            return value;
        }

        public static Vector2 ToCurrentSpace(
            this IRepositoryProvider provider,
            Vector2 value,
            CommonSpace fromSpace
        )
        {
            var service = provider?.GetService<ViewportService>();

            if (service is ViewportService viewportService)
            {
                return value.ConvertSpace(
                    fromSpace,
                    viewportService.Space,
                    viewportService.Resolution,
                    viewportService.DIPFactor,
                    viewportService.PixelFactor
                );
            }

            return value;
        }

        public static RectangleF ToCurrentSpace(
            this IRepositoryProvider provider,
            RectangleF value,
            CommonSpace fromSpace
        )
        {
            var service = provider?.GetService<ViewportService>();

            if (service is ViewportService viewportService)
            {
                return value.ConvertSpace(
                    fromSpace,
                    viewportService.Space,
                    viewportService.Resolution,
                    viewportService.DIPFactor,
                    viewportService.PixelFactor
                );
            }

            return value;
        }

        public static float ToCurrentSpace(
            this IRepositoryProvider provider,
            float value,
            CommonSpace fromSpace
        )
        {
            var service = provider?.GetService<ViewportService>();

            if (service is ViewportService viewportService)
            {
                return value.ConvertSpace(
                    fromSpace,
                    viewportService.Space,
                    viewportService.Resolution,
                    viewportService.DIPFactor,
                    viewportService.PixelFactor
                );
            }

            return value;
        }
    }
}
