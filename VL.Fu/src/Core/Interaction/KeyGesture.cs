using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Property;
using VL.Lib.IO;

namespace VL.Fu.Core.Interaction
{
    public abstract class KeyGesture : GestureBase
    {
        public override int Priority => GesturePriority.Highest;

        protected readonly CachedProperty<Keys> _key = new(Keys.None);
        protected readonly CachedProperty<Keys> _modifiers = new(Keys.None);

        public KeyGesture(IFuBehaviour behaviour, Keys key, Keys modifiers = Keys.None)
            : base(behaviour)
        {
            _key.SetValue(key);
            _modifiers.SetValue(modifiers);
        }

        protected bool CheckConditions(FuInputState inputState)
        {
            var targetKey = _key.Value;
            if (targetKey == Keys.None)
                return false;

            // 1. Check Key
            bool isKeyDown = inputState.Keys.Any(k => k.Key == targetKey);
            if (!isKeyDown)
                return false;

            // 2. Check Modifiers
            var requiredMods = _modifiers.Value;
            if (requiredMods != Keys.None)
            {
                if (
                    requiredMods.HasFlag(Keys.Control)
                    && !inputState.Modifiers.Any(k => k.Key == Keys.ControlKey)
                )
                    return false;
                if (
                    requiredMods.HasFlag(Keys.Shift)
                    && !inputState.Modifiers.Any(k => k.Key == Keys.ShiftKey)
                )
                    return false;
                if (
                    requiredMods.HasFlag(Keys.Alt)
                    && !inputState.Modifiers.Any(k => k.Key == Keys.Menu)
                )
                    return false;
            }

            return true;
        }
    }
}
