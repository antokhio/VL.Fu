using YogaSharp;

namespace Fu.Core.Styles
{
    /// <summary>
    /// Conversions between the values Fu nodes take and the values Yoga stores.
    /// Fu takes relative sizes as a ratio of the parent, 1 being the whole parent,
    /// while Yoga stores them in percent.
    /// </summary>
    public static class YogaValues
    {
        /// <summary>
        /// How many Yoga percent one unit of ratio is: a ratio of 0.5 is stored as 50 percent.
        /// </summary>
        public const float PercentPerRatio = 100f;

        /// <summary>
        /// A size relative to the parent, 1 being the whole parent.
        /// NaN or infinity give <see cref="YGValue.Undefined"/>, like <see cref="YGValue.Percent"/>.
        /// </summary>
        public static YGValue Ratio(float ratio) => YGValue.Percent(ratio * PercentPerRatio);
    }
}
