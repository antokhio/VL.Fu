using VL.Fu.Core;
using VL.Lib.Collections;

namespace VL.Fu.Helpers
{
    public static class KeyHelper
    {
        public static string FormatKeyHelper(Spread<FuKey> keys, Spread<FuKey> modifiers)
        {
            var modNames = modifiers.Select(k => k.Name);
            var keyNames = keys.Select(k => k.Name);

            if (!modNames.Any() && !keyNames.Any())
                return string.Empty;

            if (modNames.Any() && keyNames.Any())
                return string.Join(" + ", modNames) + " + " + string.Join(", ", keyNames);

            if (modNames.Any())
                return string.Join(" + ", modNames);

            return string.Join(", ", keyNames);
        }
    }
}
