using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga position type: how the position offsets (<see cref="PositionStyle"/>) place the node.
    /// Relative, the default, moves the node from its place in the flow without moving its siblings.
    /// Absolute takes the node out of the flow and places it inside its parent's padding box.
    /// Static ignores the offsets.
    /// </summary>
    public sealed record PositionTypeStyle(IReadOnlyList<IStyle> Inputs, YGPositionType PositionType) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(PositionType, nameof(PositionType));
            node->SetPositionType(PositionType);
        }
    }
}
