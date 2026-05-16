using System.Collections;
using Stride.Core.Mathematics;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Selection;
using VL.Lib.IO.Notifications;
using VL.Skia;
using YogaSharp;

namespace VL.Fu
{
    public class PrecomposeNode : IFuNode
    {
        private IFuNode? _input;

        public IEnumerable<ITreeNode> Children => _input?.Children ?? [];

        public ITreeNode? Parent
        {
            get => _input?.Parent;
            set
            {
                if (_input != null)
                    _input.Parent = value;
                else
                    return;
            }
        }

        public RectangleF? Bounds => _input?.Bounds;

        public IReadOnlyList<IFuBehaviour> Behaviours => _input?.Behaviours ?? [];

        public int InstanceId => _input?.InstanceId ?? 0;

        public unsafe YGNode* Handle => _input?.Handle;

        public RectangleF Layout => _input?.Layout ?? RectangleF.Empty;

        public void ApplyLayout(LayoutableCalculateLayoutArgs args, RectangleF? ownerLayout)
        {
            _input?.ApplyLayout(args, ownerLayout);
        }

        public IEnumerator<ITreeNode> GetEnumerator()
        {
            return Children.GetEnumerator();
        }

        public bool HitTest(FuPointer pointer)
        {
            return _input?.HitTest(pointer) ?? false;
        }

        public bool IsContainedIn(ISelectionShape shape)
        {
            return _input?.IsContainedIn(shape) ?? false;
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            return _input?.Notify(notification, caller) ?? false;
        }

        public void Render(CallerInfo caller)
        {
            _input?.Render(caller);
        }

        public void SetChildren(IEnumerable<ITreeNode> newChildren)
        {
            _input?.SetChildren(newChildren);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
