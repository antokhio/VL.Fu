using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Property;
using VL.Fu.Core.Selection;
using VL.Fu.Interaction.Gestures;
using VL.Lib.IO;
using VL.Lib.Reactive;

namespace VL.Fu.Interaction.Selection
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class Selector : BehaviourBase, IFuBehaviour
    {
        public override bool IsTransient => false;
        public override int Priority => GesturePriority.Click;

        [Fragment]
        public bool IsSelected => _isSelectedChannel.Value;

        private readonly ChannelProperty<bool> _isSelectedChannel = new(false);

        [Fragment]
        public Selector(NodeContext nodeContext)
            : base(nodeContext)
        {
            Gestures = [new LeftClickGesture(this)];
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetIsSelectedChannel(IChannel<bool>? channel) =>
            _isSelectedChannel.SetChannel(channel);

        public void SetSelected(bool selected) => _isSelectedChannel.OnNext(selected);

        public override void OnFinish(IFuNode host, FuGestureEvent ev)
        {
            var provider = host.FindSelectionProvider();
            if (provider == null)
                return;

            bool isAdd = ev.InputState.Modifiers.Any(k => k.Key == Keys.ControlKey);
            bool isRemove = ev.InputState.Modifiers.Any(k => k.Key == Keys.Menu);

            provider.Select(host, isAdd, isRemove);
        }
    }
}
