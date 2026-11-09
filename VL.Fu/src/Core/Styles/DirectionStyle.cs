using VL.Fu.Core.Styling;
using YogaSharp;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga layout direction of this node and, through Inherit, of its subtree.
    /// It decides whether rows run left to right or right to left and which side Start and End edges land on.
    /// Inherit, the default, takes the parent's direction; a root inherits left to right.
    /// </summary>
    public sealed record DirectionStyle(IReadOnlyList<IStyle> Inputs, YGDirection Direction) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node)
        {
            YogaEnums.EnsureSupported(Direction, nameof(Direction));
            node->SetDirection(Direction);
        }
    }
}
