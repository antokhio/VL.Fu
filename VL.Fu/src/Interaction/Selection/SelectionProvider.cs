using System.Collections.Immutable;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Property;
using VL.Fu.Core.Selection;
using VL.Fu.Extensions;
using VL.Fu.Interaction.Gestures;
using VL.Lib.IO;
using VL.Lib.Reactive;

namespace VL.Fu.Interaction.Selection
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class SelectionProvider : BehaviourBase, IFuBehaviour, ISelectionProvider
    {
        [Fragment]
        public RectangleF SelectionRect => _selectionRectChannel.Value;

        public IReadOnlySet<IFuNode> SelectedNodes => _selectedNodes;

        [Fragment]
        public bool IsSelecting => _isSelectingChannel.Value;

        private readonly ChannelProperty<RectangleF> _selectionRectChannel = new(RectangleF.Empty);
        private readonly ChannelProperty<bool> _isSelectingChannel = new(false);
        private readonly MarqueeGesture _gesture;

        private readonly HashSet<IFuNode> _selectedNodes = new();

        // Cache
        private struct Candidate
        {
            public IFuNode Node;
            public bool WasSelected;
        }

        private readonly List<Candidate> _candidates = new();

        [Fragment]
        public SelectionProvider(NodeContext nodeContext)
            : base(nodeContext)
        {
            _gesture = new MarqueeGesture(this);
            Gestures = [_gesture];
        }

        [Fragment(Order = PinOrder.Action)]
        public void SetSelectionRectChannel(IChannel<RectangleF>? c) =>
            _selectionRectChannel.SetChannel(c);

        [Fragment(Order = PinOrder.Action)]
        public void SetIsSelectingChannel(IChannel<bool>? c) => _isSelectingChannel.SetChannel(c);

        // --- ISelectionProvider ---

        public bool IsSelected(IFuNode node) => _selectedNodes.Contains(node);

        public void Select(IFuNode node, bool add, bool remove)
        {
            if (add)
            {
                if (_selectedNodes.Add(node))
                    NotifyNode(node, true);
            }
            else if (remove)
            {
                if (_selectedNodes.Remove(node))
                    NotifyNode(node, false);
            }
            else
            {
                // Exclusive
                foreach (var n in _selectedNodes)
                    if (n != node)
                        NotifyNode(n, false);
                _selectedNodes.Clear();
                _selectedNodes.Add(node);
                NotifyNode(node, true);
            }
        }

        public void DispatchToSelected(Action<IFuNode> action, IFuNode? exclude = null)
        {
            foreach (var node in _selectedNodes)
            {
                if (node == exclude)
                    continue;
                action(node);
            }
        }

        private void NotifyNode(IFuNode node, bool isSelected)
        {
            // We notify known selection-aware behaviors
            var selector = node.Behaviours.OfType<Selector>().FirstOrDefault();
            selector?.SetSelected(isSelected);

            var movable = node.Behaviours.OfType<Movable>().FirstOrDefault();
            movable?.SetSelected(isSelected);
        }

        // --- Marquee Logic ---

        public override void OnStart(IFuNode host, FuGestureEvent ev)
        {
            _isSelectingChannel.OnNext(true);
            _candidates.Clear();

            foreach (var treeNode in host.TraverseBreadthFirst())
            {
                if (treeNode == host)
                    continue;

                if (treeNode is IFuNode node)
                    if (node.Behaviours.Any(b => b is Selector && b.Enabled))
                    {
                        _candidates.Add(
                            new Candidate
                            {
                                Node = (IFuNode)treeNode,
                                WasSelected = _selectedNodes.Contains(treeNode),
                            }
                        );
                    }
            }
            UpdateMarquee(ev.InputState);
        }

        public override void OnUpdate(IFuNode host, FuGestureEvent ev) =>
            UpdateMarquee(ev.InputState);

        public override void OnFinish(IFuNode host, FuGestureEvent ev)
        {
            UpdateMarquee(ev.InputState);
            Reset();
        }

        public override void OnCancel(IFuNode host, FuGestureEvent ev)
        {
            foreach (var c in _candidates)
            {
                if (_selectedNodes.Contains(c.Node) != c.WasSelected)
                    Select(c.Node, !c.WasSelected, c.WasSelected);
            }
            Reset();
        }

        private void Reset()
        {
            _isSelectingChannel.OnNext(false);
            _selectionRectChannel.OnNext(RectangleF.Empty);
            _candidates.Clear();
        }

        private void UpdateMarquee(FuInputState input)
        {
            var shape = new MarqueeShape(_gesture.StartPosition, _gesture.CurrentPosition);
            _selectionRectChannel.OnNext(shape.Rect);

            bool isAdd = input.Modifiers.Any(k => k.Key == Keys.ControlKey);
            bool isRemove = input.Modifiers.Any(k => k.Key == Keys.Menu);

            foreach (var c in _candidates)
            {
                bool isHit = c.Node.IsContainedIn(shape);
                bool targetState = isHit;

                if (isAdd)
                    targetState = c.WasSelected || isHit;
                else if (isRemove)
                    targetState = c.WasSelected && !isHit;

                if (_selectedNodes.Contains(c.Node) != targetState)
                {
                    Select(c.Node, targetState, !targetState);
                }
            }
        }
    }
}
