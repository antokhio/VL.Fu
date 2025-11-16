using Stride.Core.Mathematics;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core.Notfications
{
    public class ViewportBoundsNotification : NotificationBase, INotification
    {
        public RectangleF ViewportBounds { get; }
        public float Scaling { get; }

        public ViewportBoundsNotification(
            RectangleF viewportBounds,
            float scaling,
            object sender,
            Keys modifierKeys = Keys.None
        )
            : base(sender, modifierKeys)
        {
            ViewportBounds = viewportBounds;
        }

        public override INotification Transform(INotificationSpaceTransformer transformer)
        {
            throw new NotImplementedException();
        }

        public override INotification WithSender(object sender)
        {
            return new ViewportBoundsNotification(ViewportBounds, Scaling, sender, ModifierKeys);
        }
    }
}
