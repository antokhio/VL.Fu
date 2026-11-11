using VL.Fu.Core.Interaction;
using VL.Fu.Core.Styling;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Whether hit testing considers a node and its subtree.
    /// </summary>
    public enum PointerEvents
    {
        /// <summary>
        /// The node and its subtree take part in hit testing, so a pointer over them reaches them
        /// and doesn't reach the siblings behind them.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Hit testing skips the node and its subtree, so a pointer over them reaches what is behind them.
        /// Control notifications, like a cancel of a running gesture, still reach them.
        /// </summary>
        None = 1,
    }

    /// <summary>
    /// Sets whether hit testing considers the node and its subtree.
    /// Without it <see cref="PointerEvents.Auto"/> holds.
    /// </summary>
    /// <remarks>
    /// The pass commits only when the styled value changed, like <see cref="TransformOriginStyle"/>,
    /// so a value set directly on the node is kept until the style changes it.
    /// </remarks>
    public sealed record PointerEventsStyle(IReadOnlyList<IStyle> Inputs, PointerEvents PointerEvents) : Style<IFuNode>(Inputs)
    {
        internal static bool IsSupported(PointerEvents value) =>
            value is PointerEvents.Auto or PointerEvents.None;

        public override void Build(IFuNode node)
        {
            if (!IsSupported(PointerEvents))
                throw new ArgumentOutOfRangeException(nameof(PointerEvents), PointerEvents, $"PointerEvents can't be {PointerEvents}.");

            // A node only styles itself, so an IFuNode here is a Fu node, which is interactive.
            ((Interactive<IFuNode>)node).StagePointerEvents(PointerEvents);
        }
    }
}
