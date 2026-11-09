using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Writes a <see cref="YGValue"/> into a width, height, flex basis, min or max slot, choosing the Yoga setter by its unit.
    /// Shared by the size records, so the same slot kind always accepts exactly the same units.
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

        /// <summary>
        /// Throws for units a min or max slot can't take. Unlike width and height, these have no auto.
        /// </summary>
        public static void EnsureSupportedBound(YGValue value, string name)
        {
            if (value.Unit is not (YGUnit.Point or YGUnit.Percent))
                throw new ArgumentOutOfRangeException(name, value.Unit, $"Unsupported {name.ToLowerInvariant()} unit.");
        }

        public static void SetFlexBasis(YGNode* node, YGValue flexBasis)
        {
            EnsureSupported(flexBasis, "FlexBasis");

            switch (flexBasis.Unit)
            {
                case YGUnit.Point:
                    node->SetFlexBasis(flexBasis.Value);
                    break;
                case YGUnit.Percent:
                    node->SetFlexBasisPercent(flexBasis.Value);
                    break;
                default:
                    node->SetFlexBasisAuto();
                    break;
            }
        }

        public static void SetMinWidth(YGNode* node, YGValue minWidth)
        {
            EnsureSupportedBound(minWidth, "MinWidth");

            if (minWidth.Unit == YGUnit.Point)
                node->SetMinWidth(minWidth.Value);
            else
                node->SetMinWidthPercent(minWidth.Value);
        }

        public static void SetMaxWidth(YGNode* node, YGValue maxWidth)
        {
            EnsureSupportedBound(maxWidth, "MaxWidth");

            if (maxWidth.Unit == YGUnit.Point)
                node->SetMaxWidth(maxWidth.Value);
            else
                node->SetMaxWidthPercent(maxWidth.Value);
        }

        public static void SetMinHeight(YGNode* node, YGValue minHeight)
        {
            EnsureSupportedBound(minHeight, "MinHeight");

            if (minHeight.Unit == YGUnit.Point)
                node->SetMinHeight(minHeight.Value);
            else
                node->SetMinHeightPercent(minHeight.Value);
        }

        public static void SetMaxHeight(YGNode* node, YGValue maxHeight)
        {
            EnsureSupportedBound(maxHeight, "MaxHeight");

            if (maxHeight.Unit == YGUnit.Point)
                node->SetMaxHeight(maxHeight.Value);
            else
                node->SetMaxHeightPercent(maxHeight.Value);
        }
    }
}
