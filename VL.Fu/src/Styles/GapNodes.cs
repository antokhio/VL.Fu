using VL.Fu.Core.Base;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Shared state of the gap setters: one cached value and gutter, rebuilt once per change.
    /// Without a value the gap is not set here, so an earlier setter or the default holds.
    /// </summary>
    [ProcessNode]
    public abstract class GapStyleNode : StyleNode
    {
        private Optional<float> _gap;
        private YGGutter _gutter;

        protected GapStyleNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext, YGGutter gutter)
            : base(nodeContext)
        {
            _gutter = gutter;
            RebuildStyle();
        }

        /// <summary>
        /// Stores the gap and the gutter, correcting an unknown gutter to <see cref="YGGutter.All"/>,
        /// and rebuilds the output if either changed.
        /// </summary>
        protected void SetGapCore(Optional<float> gap, YGGutter gutter)
        {
            if (!Enum.IsDefined(gutter))
            {
                this.LogWarning($"Unknown gutter {gutter}. All is used instead.");
                gutter = YGGutter.All;
            }

            var changed = SetValue(ref _gap, ValidateFinite(gap, "Gap"));
            changed |= SetValue(ref _gutter, gutter);
            if (changed)
                RebuildStyle();
        }

        protected sealed override Style? BuildStyle(IReadOnlyList<Style> inputs) =>
            _gap.HasValue ? new GapStyle(inputs, _gutter, _gap.Value) : null;
    }

    /// <summary>
    /// Sets the gap between children for the given gutter, all of them by default.
    /// </summary>
    [ProcessNode(Name = "SetGap")]
    public sealed class SetGapNode : GapStyleNode
    {
        public SetGapNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext, YGGutter.All) { }

        public void SetGap(Optional<float> gap = default, YGGutter gutter = YGGutter.All) => SetGapCore(gap, gutter);
    }

    /// <summary>
    /// Sets the gap between rows of children, beating the gap for all gutters.
    /// </summary>
    [ProcessNode(Name = "SetRowGap")]
    public sealed class SetRowGapNode : GapStyleNode
    {
        public SetRowGapNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext, YGGutter.Row) { }

        public void SetRowGap(Optional<float> gap = default) => SetGapCore(gap, YGGutter.Row);
    }

    /// <summary>
    /// Sets the gap between columns of children, beating the gap for all gutters.
    /// </summary>
    [ProcessNode(Name = "SetColumnGap")]
    public sealed class SetColumnGapNode : GapStyleNode
    {
        public SetColumnGapNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext, YGGutter.Column) { }

        public void SetColumnGap(Optional<float> gap = default) => SetGapCore(gap, YGGutter.Column);
    }
}
