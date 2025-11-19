using VL.Fu.Core.Input;

namespace VL.Fu.Core
{
    /// <summary>
    /// Defines the contract for a service that provides viewport information and handles related notifications.
    /// </summary>
    public interface IViewportService : IContextedService, INotifiable
    {
        /// <summary>
        /// Gets the current state of the viewport, including resolution, scaling, and coordinate space information.
        /// </summary>
        FuViewport Viewport { get; }
    }
}
