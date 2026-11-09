using VL.Fu.Core.Base;
using YogaSharp;

namespace VL.Fu.Core.Styling
{
    /// <summary>
    /// The Yoga subset of <see cref="Style"/>: writes only Yoga style, into a native Yoga node.
    /// A layoutable node runs all Yoga instructions of a pass on a shared stage node and copies the result once,
    /// so Yoga marks the node dirty only when its style really changed.
    /// </summary>
    /// <remarks>
    /// The result depends only on the instructions, never on the node, so an instruction can't read anything from the Fu node.
    /// Yoga's own specificity still applies regardless of order: a left margin beats the margin for all edges,
    /// a row gap beats the gap for all gutters.
    /// </remarks>
    public abstract record YogaStyle(IReadOnlyList<Style> Inputs) : Style(Inputs)
    {
        /// <summary>
        /// Writes this instruction into the style of <paramref name="node"/>, the stage of the running pass.
        /// Must only set style; layout results, children and the config of the stage are not the node's.
        /// </summary>
        public abstract unsafe void Build(YGNode* node);

        /// <summary>
        /// Reached only for a node without Yoga, since layoutable nodes handle Yoga instructions before this.
        /// The pass reports the exception on the node and goes on.
        /// </summary>
        public sealed override void Build(NodeBase node) =>
            throw new NotSupportedException($"{GetType().Name} requires a layoutable node.");
    }
}
