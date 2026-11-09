using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets how the children and the free space are placed along the main axis.
    /// Without a value it is not set here, so an earlier setter or the default, flex start, holds.
    /// </summary>
    [ProcessNode(Name = "SetJustifyContent")]
    public sealed class SetJustifyContentNode : StyleNode
    {
        private Optional<YGJustify> _justifyContent;

        public SetJustifyContentNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetJustifyContent(Optional<YGJustify> justifyContent = default)
        {
            if (SetValue(ref _justifyContent, ValidateEnum(justifyContent, "JustifyContent")))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _justifyContent.HasValue ? new JustifyContentStyle(inputs, _justifyContent.Value) : null;
    }

    /// <summary>
    /// Sets how the children are placed along the cross axis of their line.
    /// Only flex start, center, flex end, stretch and baseline are accepted; other values are ignored.
    /// Without a value it is not set here, so an earlier setter or the default, stretch, holds.
    /// </summary>
    [ProcessNode(Name = "SetAlignItems")]
    public sealed class SetAlignItemsNode : StyleNode
    {
        private Optional<YGAlign> _alignItems;

        public SetAlignItemsNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetAlignItems(Optional<YGAlign> alignItems = default)
        {
            if (SetValue(ref _alignItems, ValidateEnum(alignItems, "AlignItems", AlignItemsStyle.IsSupported)))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _alignItems.HasValue ? new AlignItemsStyle(inputs, _alignItems.Value) : null;
    }

    /// <summary>
    /// Sets how this node is placed along the cross axis of its line, beating the parent's align items.
    /// Auto and the align-items values are accepted; other values are ignored.
    /// Without a value it is not set here, so an earlier setter or the default, auto, holds.
    /// </summary>
    [ProcessNode(Name = "SetAlignSelf")]
    public sealed class SetAlignSelfNode : StyleNode
    {
        private Optional<YGAlign> _alignSelf;

        public SetAlignSelfNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetAlignSelf(Optional<YGAlign> alignSelf = default)
        {
            if (SetValue(ref _alignSelf, ValidateEnum(alignSelf, "AlignSelf", AlignSelfStyle.IsSupported)))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _alignSelf.HasValue ? new AlignSelfStyle(inputs, _alignSelf.Value) : null;
    }

    /// <summary>
    /// Sets how the lines of a wrapping node are placed along the cross axis.
    /// Auto and baseline are ignored. Without a value it is not set here, so an earlier setter or the default, stretch, holds.
    /// </summary>
    [ProcessNode(Name = "SetAlignContent")]
    public sealed class SetAlignContentNode : StyleNode
    {
        private Optional<YGAlign> _alignContent;

        public SetAlignContentNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetAlignContent(Optional<YGAlign> alignContent = default)
        {
            if (SetValue(ref _alignContent, ValidateEnum(alignContent, "AlignContent", AlignContentStyle.IsSupported)))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _alignContent.HasValue ? new AlignContentStyle(inputs, _alignContent.Value) : null;
    }
}
