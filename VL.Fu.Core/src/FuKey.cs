using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Core
{
    public record struct FuKey
    {
        public string Name => Key.ToKeyName();
        public Keys Key { get; set; }
        public KeyNotificationKind State { get; set; }

        public bool IsModifier() =>
            Key == Keys.ControlKey || Key == Keys.ShiftKey || Key == Keys.Menu;

        public DateTimeOffset CreatedAt { get; }
        public TimeSpan LifeSpan => DateTimeOffset.UtcNow - CreatedAt;

        public FuKey() { }

        public FuKey(Keys key, KeyNotificationKind state)
        {
            Key = key;
            State = state;
        }

        public FuKey(string name, KeyNotificationKind state)
        {
            Key = KeyboardNodes.FromKeyName(name);
            State = state;
        }
    }
}
