using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Model;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets whether hit testing considers the node and its subtree.
    /// None lets the pointer reach what is behind them; control notifications, like a cancel of a running gesture, still reach them.
    /// Without a value it is not set here, so an earlier setter or the default, Auto, holds.
    /// </summary>
    [ProcessNode(Name = "SetPointerEvents")]
    public sealed class SetPointerEventsNode : StyleNode
    {
        private Optional<PointerEvents> _pointerEvents;

        public SetPointerEventsNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetPointerEvents(Optional<PointerEvents> pointerEvents = default)
        {
            if (SetValue(ref _pointerEvents, ValidateEnum(pointerEvents, "PointerEvents", PointerEventsStyle.IsSupported)))
                RebuildStyle();
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _pointerEvents.HasValue ? new PointerEventsStyle(inputs, _pointerEvents.Value) : null;
    }
}
