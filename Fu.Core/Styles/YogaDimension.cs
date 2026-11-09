using YogaSharp;

namespace Fu.Core.Styles
{
    /// <summary>
    /// Writes a <see cref="YGValue"/> into a width or height slot, choosing the Yoga setter by its unit.
    /// Shared by the width, height and size styles, so they accept exactly the same units.
    /// </summary>
    internal static unsafe class YogaDimension
    {
        /// <summary>
        /// Throws for units a width or height slot can't take, so a style can check all of its values
        /// before writing any of them and is skipped as a whole.
        /// </summary>
        public static void EnsureSupported(YGValue value, string name)
        {
            if (value.Unit is not (YGUnit.Point or YGUnit.Percent or YGUnit.Auto))
                throw new ArgumentOutOfRangeException(name, value.Unit, $"Unsupported {name.ToLowerInvariant()} unit.");
        }

        public static void SetWidth(YGNode* node, YGValue width)
        {
            EnsureSupported(width, "Width");

            switch (width.Unit)
            {
                case YGUnit.Point:
                    node->SetWidth(width.Value);
                    break;
                case YGUnit.Percent:
                    node->SetWidthPercent(width.Value);
                    break;
                default:
                    node->SetWidthAuto();
                    break;
            }
        }

        public static void SetHeight(YGNode* node, YGValue height)
        {
            EnsureSupported(height, "Height");

            switch (height.Unit)
            {
                case YGUnit.Point:
                    node->SetHeight(height.Value);
                    break;
                case YGUnit.Percent:
                    node->SetHeightPercent(height.Value);
                    break;
                default:
                    node->SetHeightAuto();
                    break;
            }
        }
    }
}
