using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Behaviors
{
    /// <summary>
    /// Makes the node it is attached to an item of the nearest <see cref="SelectionProvider"/> above it.
    /// Connect a channel to <see cref="IsSelected"/>: it is the truth, and both the patch and the provider write it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pressing an unselected item selects it and replaces the selection; Shift adds it, Ctrl toggles it. Clicking an item that was
    /// selected at the press collapses the selection to it; Ctrl deselects it, Shift keeps everything. In
    /// <see cref="SelectionMode.Single"/>, Shift and Ctrl on an unselected item replace like a plain press.
    /// </para>
    /// <para>
    /// The press never asks for the pointer, so a drag on the same node still wins. Whatever the press selected stays selected
    /// when a drag takes the pointer. A click on an item is reported as processed, so it doesn't count as a click on the background.
    /// Without a provider above it, a click toggles the item alone.
    /// </para>
    /// </remarks>
    [ProcessNode(Name = "Selectable", HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public sealed class Selectable : NodeBase, IBehavior
    {
        private readonly ClickGestureRecognizer _click = new();

        private IChannel<bool>? _isSelected = null;
        private bool _enabled = true;

        private bool _selectedAtPress = false;
        private bool _warnedNoProvider = false;

        [Fragment]
        public Selectable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// Whether the item is selected. The patch may write it too; the provider only changes it on user interaction.
        /// </summary>
        public IChannel<bool>? IsSelected => _isSelected;

        /// <summary>
        /// Connects the channel that holds whether the item is selected. Changing it forgets a press in progress.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetIsSelected(IChannel<bool>? isSelected)
        {
            if (ReferenceEquals(isSelected, _isSelected))
                return;

            _click.Cancel();
            _isSelected = isSelected;
        }

        public bool Enabled => _enabled;

        /// <summary>
        /// Disabling forgets a press in progress. A disabled item keeps its state and ignores the provider.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            if (enabled == _enabled)
                return;

            if (!enabled)
                _click.Cancel();

            _enabled = enabled;
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            if (!_enabled)
                return false;

            switch (notification)
            {
                case SelectionNotification selection:
                    Apply(selection);
                    return false;
                case PointerCancelNotification cancel:
                    var current = _click.Current;
                    if (current.Phase != ClickPhase.Idle && current.PointerId == cancel.PointerId && !cancel.IsMember(this))
                        _click.Cancel();
                    return false;
            }

            if (!PointerInput.TryRead(notification, out var sample))
                return false;

            var click = _click.Process(in sample);
            switch (click.Phase)
            {
                case ClickPhase.Pressed:
                    OnPressed(notification, in sample);
                    return false;
                case ClickPhase.Clicked:
                    return OnClicked(notification, in sample);
                default:
                    return false;
            }
        }

        private bool Selected => _isSelected?.Value ?? false;

        /// <summary>
        /// Whether an enabled <see cref="Selectable"/> on <paramref name="node"/> reports it selected.
        /// A disabled one stays out of its provider, so it doesn't count.
        /// </summary>
        internal static bool IsSelectedOn(IInteractive node)
        {
            var behaviors = node.Behaviors;
            for (var i = 0; i < behaviors.Count; i++)
            {
                if (behaviors[i] is Selectable { _enabled: true, Selected: true })
                    return true;
            }

            return false;
        }

        /// <summary>
        /// An unselected item selects at once (S2), so a drag that follows moves it. A selected one waits for the click,
        /// so a drag that follows keeps the selection.
        /// </summary>
        private void OnPressed(INotification notification, in PointerSample sample)
        {
            _selectedAtPress = Selected;
            if (!SelectionProvider.TryFind(sample.Node, out var provider, out var providerNode) || !provider.Enabled)
                return;

            provider.NoteItemPress(sample.PointerId);
            if (_selectedAtPress)
                return;

            var action =
                provider.Mode == SelectionMode.Single ? SelectionAction.Replace
                : Has(sample.Modifiers, Keys.Control) ? SelectionAction.Toggle
                : Has(sample.Modifiers, Keys.Shift) ? SelectionAction.Add
                : SelectionAction.Replace;
            provider.Send(notification, providerNode, action, sample.Node, sample.Modifiers);
        }

        private bool OnClicked(INotification notification, in PointerSample sample)
        {
            if (!SelectionProvider.TryFind(sample.Node, out var provider, out var providerNode))
            {
                if (!_warnedNoProvider)
                {
                    _warnedNoProvider = true;
                    this.LogWarning("No SelectionProvider above this item, so a click only toggles the item itself.");
                }

                _isSelected?.EnsureValue(!Selected);
                return true;
            }

            if (!provider.Enabled)
                return false;

            if (!_selectedAtPress || Has(sample.Modifiers, Keys.Shift))
                return true;

            var action = Has(sample.Modifiers, Keys.Control) ? SelectionAction.Toggle : SelectionAction.Replace;
            provider.Send(notification, providerNode, action, sample.Node, sample.Modifiers);
            return true;
        }

        private void Apply(SelectionNotification selection)
        {
            if (selection.Sender is not FuNotificationSender { Node: var node } || !selection.Concerns(node))
                return;

            var isTarget = ReferenceEquals(node, selection.Target);
            switch (selection.Action)
            {
                case SelectionAction.Clear:
                    _isSelected?.EnsureValue(false);
                    break;
                case SelectionAction.Replace:
                    _isSelected?.EnsureValue(isTarget);
                    break;
                case SelectionAction.Add when isTarget:
                    _isSelected?.EnsureValue(true);
                    break;
                case SelectionAction.Toggle when isTarget:
                    _isSelected?.EnsureValue(!Selected);
                    break;
                case SelectionAction.Marquee:
                    var inside =
                        selection.TryGetItemBounds(out var bounds) && SelectionNotification.Contains(selection.Area, bounds);
                    if (Has(selection.ModifierKeys, Keys.Control))
                    {
                        if (inside)
                            _isSelected?.EnsureValue(!Selected);
                    }
                    else if (Has(selection.ModifierKeys, Keys.Shift))
                    {
                        if (inside)
                            _isSelected?.EnsureValue(true);
                    }
                    else
                    {
                        _isSelected?.EnsureValue(inside);
                    }
                    break;
                case SelectionAction.CollectBounds when Selected:
                    if (selection.TryGetItemBounds(out var own))
                        selection.IncludeBounds(own);
                    break;
            }
        }

        private static bool Has(Keys modifiers, Keys key) => (modifiers & key) == key;
    }
}
