using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Core
{
    public interface ILayoutable
    {
        unsafe YGNode* Handle { get; }
        RectangleF Layout { get; }

        void ApplyLayout(LayoutableCalculateLayoutArgs args, RectangleF? ownerLayout);
    }

    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Layoutable : TreeNode, ILayoutable
    {
        protected unsafe YGNode* _handle = YGNode.New();
        public unsafe YGNode* Handle => _handle;

        private RectangleF _layout = RectangleF.Empty;
        public RectangleF Layout
        {
            get => _layout;
            protected set => _layout = value;
        }

        [Fragment]
        protected Layoutable(NodeContext nodeContext)
            : base(nodeContext) { }

        public override void SetChildren(
            [Pin(PinGroupKind = PinGroupKind.Collection, PinGroupDefaultCount = 1)]
                Spread<IFuNode> children
        )
        {
            base.SetChildren(children);

            var validChidren = new List<IFuNode>();
            foreach (var child in children)
            {
                if (child is IFuNode node)
                {
                    validChidren.Add(node);
                }
            }

            unsafe
            {
                YGNode*[] childHandles = new YGNode*[validChidren.Count];
                for (int i = 0; i < validChidren.Count; i++)
                {
                    childHandles[i] = validChidren[i].Handle;
                }

                _handle->SetChildren(childHandles);
            }
        }

        public virtual void ApplyLayout(LayoutableCalculateLayoutArgs args, RectangleF? ownerLayout)
        {
            this.BuildLayout(ownerLayout ?? args.OwnerBounds);

            foreach (var child in Children)
            {
                if (child is Layoutable layoutableChild)
                {
                    layoutableChild.ApplyLayout(args, Layout);
                }
            }

            // TODO:
            // OnLayoutChanged

            this.SetHasNewLayout(false);
        }

        protected virtual unsafe void BuildLayout(RectangleF? ownerLayout)
        {
            var left = _handle->GetComputedLeft() + ownerLayout?.Left ?? 0f;
            var top = _handle->GetComputedTop() + ownerLayout?.Top ?? 0f;
            var width = _handle->GetComputedWidth();
            var height = _handle->GetComputedHeight();

            Layout = _layout with { X = left, Y = top, Width = width, Height = height };
        }

        public override void Dispose()
        {
            unsafe
            {
                _handle = (YGNode*)IntPtr.Zero;
            }

            base.Dispose();
        }
    }
}
