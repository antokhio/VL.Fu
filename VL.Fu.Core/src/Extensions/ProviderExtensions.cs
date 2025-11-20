using Stride.Core.Mathematics;
using VL.Fu.Core.Context;
using VL.Fu.Core.Input;
using VL.Lib.Mathematics;
using VL.Skia;

namespace VL.Fu.Core.Extensions
{
    /// <summary>
    /// Provides high-level extension methods for <see cref="IContextProvider"/> to simplify coordinate conversions.
    /// </summary>
    public static class ProviderExtensions
    {
        public static bool TryGetPointersStream(
            this IContextProvider provider,
            out IObservable<IReadOnlyDictionary<int, FuPointer>>? pointersStream
        )
        {
            var service = provider?.GetService<IInputService>();
            pointersStream = service?.PointersStream;

            return pointersStream is not null;
        }

        public static bool TryGetMouseStream(
            this IContextProvider provider,
            out IObservable<FuMouse>? mouseStream
        )
        {
            var service = provider?.GetService<IInputService>();
            mouseStream = service?.MouseStream;

            return mouseStream is not null;
        }

        public static bool TryGetKeysStream(
            this IContextProvider provider,
            out IObservable<IReadOnlySet<FuKey>>? keysStream
        )
        {
            var service = provider?.GetService<IInputService>();
            keysStream = service?.KeysStream;

            return keysStream is not null;
        }

        public static bool TryGetViewportStream(
            this IContextProvider provider,
            out IObservable<FuViewport>? viewportStream
        )
        {
            var service = provider?.GetService<IViewportService>();
            viewportStream = service?.ViewportStream;

            return viewportStream is not null;
        }

        /// <summary>
        /// Retrieves the FuViewport from the provider's service registry.
        /// </summary>
        /// <param name="provider">The context provider.</param>
        /// <returns>The FuViewport if the service is available, otherwise null.</returns>
        public static FuViewport? GetViewport(this IContextProvider provider)
        {
            return provider.GetService<IViewportService>()?.Viewport;
        }

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
            var viewport = provider.GetViewport();
            return viewport?.ConvertSize(value, fromSpace) ?? value;
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
            var viewport = provider.GetViewport();
            return viewport?.ConvertPosition(value, fromSpace) ?? value;
        }

        /// <summary>
        /// Converts a Circle from a specified source space to the provider's current viewport space.
        /// </summary>
        /// <param name="provider">The context provider, used to access the ViewportService.</param>
        /// <param name="value">The circle to convert.</param>
        /// <param name="fromSpace">The coordinate space of the input circle.</param>
        /// <returns>The converted circle, or the original circle if the conversion cannot be performed.</returns>
        public static Circle ConvertSpace(
            this IContextProvider provider,
            Circle value,
            CommonSpace fromSpace
        )
        {
            var viewport = provider.GetViewport();
            return viewport?.ConvertSpace(value, fromSpace) ?? value;
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
            var viewport = provider.GetViewport();
            return viewport?.ConvertSpace(value, fromSpace) ?? value;
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
            var viewport = provider.GetViewport();
            return viewport?.ConvertSpace(value, fromSpace) ?? value;
        }
    }
}
