using Stride.Core.Mathematics;
using VL.Fu.Core.Context;
using VL.Skia;

namespace VL.Fu.Core.Extensions
{
    /// <summary>
    /// Provides high-level extension methods for <see cref="IContextProvider"/> to simplify coordinate conversions.
    /// </summary>
    public static class ProviderExtensions
    {
        /// <summary>
        /// Converts a Vector2 from a specified space to the provider's current viewport space.
        /// </summary>
        /// <param name="provider">The context provider.</param>
        /// <param name="value">The vector to convert.</param>
        /// <param name="fromSpace">The source coordinate space of the vector.</param>
        /// <returns>The converted vector in the provider's viewport space. Returns the original vector if the conversion cannot be performed.</returns>
        public static Vector2 ConvertSize(
            this IContextProvider provider,
            Vector2 value,
            CommonSpace fromSpace
        )
        {
            var viewportService = provider.GetService<IViewportService>();
            if (viewportService is null)
                return value;

            return viewportService.Viewport.ConvertSize(value, fromSpace);
        }

        /// <summary>
        /// Converts a Vector2 from a specified space to the provider's current viewport space.
        /// </summary>
        /// <param name="provider">The context provider.</param>
        /// <param name="value">The vector to convert.</param>
        /// <param name="fromSpace">The source coordinate space of the vector.</param>
        /// <returns>The converted vector in the provider's viewport space. Returns the original vector if the conversion cannot be performed.</returns>
        public static Vector2 ConvertPosition(
            this IContextProvider provider,
            Vector2 value,
            CommonSpace fromSpace
        )
        {
            var viewportService = provider.GetService<IViewportService>();
            if (viewportService is null)
                return value;

            return viewportService.Viewport.ConvertPosition(value, fromSpace);
        }

        /// <summary>
        /// Converts a RectangleF from a specified space to the provider's current viewport space.
        /// </summary>
        /// <param name="provider">The context provider.</param>
        /// <param name="value">The rectangle to convert.</param>
        /// <param name="fromSpace">The source coordinate space of the rectangle.</param>
        /// <returns>The converted rectangle in the provider's viewport space. Returns the original rectangle if the conversion cannot be performed.</returns>
        public static RectangleF ConvertSpace(
            this IContextProvider provider,
            RectangleF value,
            CommonSpace fromSpace
        )
        {
            var viewportService = provider.GetService<IViewportService>();
            if (viewportService is null)
                return value;

            return viewportService.Viewport.ConvertSpace(value, fromSpace);
        }

        /// <summary>
        /// Converts a scalar distance from a specified space to the provider's current viewport space.
        /// </summary>
        /// <param name="provider">The context provider.</param>
        /// <param name="value">The distance value to convert.</param>
        /// <param name="fromSpace">The source coordinate space of the distance value.</param>
        /// <returns>The converted distance in the provider's viewport space. Returns the original value if the conversion cannot be performed.</returns>
        public static float ConvertSpace(
            this IContextProvider provider,
            float value,
            CommonSpace fromSpace
        )
        {
            var viewportService = provider.GetService<IViewportService>();
            if (viewportService is null)
                return value;

            return viewportService.Viewport.ConvertSpace(value, fromSpace);
        }
    }
}
