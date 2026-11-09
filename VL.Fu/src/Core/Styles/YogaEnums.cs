namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Checks a Yoga enum value before a record hands it to Yoga, which trusts it blindly.
    /// Shared by the records with an enum value, so they reject values the same way their nodes do.
    /// </summary>
    internal static class YogaEnums
    {
        /// <summary>
        /// Throws when <paramref name="value"/> is no member of <typeparamref name="TEnum"/>
        /// or <paramref name="isSupported"/> rejects it for the slot being written.
        /// </summary>
        public static void EnsureSupported<TEnum>(TEnum value, string name, Func<TEnum, bool>? isSupported = null)
            where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(value) || (isSupported is not null && !isSupported(value)))
                throw new ArgumentOutOfRangeException(name, value, $"Unsupported {name} value.");
        }
    }
}
