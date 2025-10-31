using Stride.Core.Mathematics;
using VL.Lib.IO.Notifications;
using VL.Skia;
using VL.UI.Core;

namespace VL.Fu.Extensions
{
    public static class NotificationExtensions
    {
        public static Vector2 ToCommonSpace(
            this INotification notfication,
            CommonSpace space,
            float dipFactor,
            float pixelFactor
        )
        {
            if (notfication is NotificationWithPosition np)
            {
                var dip = DIPHelpers.DIPFactor();
                var clientArea = np.ClientArea;
                return space switch
                {
                    CommonSpace.Normalized => np.Position.ToNormalizedSpace(clientArea),
                    CommonSpace.DIP => np.Position.ToCenteredDIPSpace(clientArea, dipFactor),
                    CommonSpace.DIPTopLeft => np.Position.ToDIPTopLeftSpace(dipFactor),
                    CommonSpace.PixelTopLeft => np.Position.ToPixelTopLeftSpace(pixelFactor),
                    _ => throw new InvalidOperationException("Space not found"),
                };
            }

            throw new InvalidOperationException("Notification does not have position information.");
        }
    }
}
