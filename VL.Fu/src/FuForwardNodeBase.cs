using System.Collections;
using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Selection;
using VL.Fu.Pain;
using VL.Lib.IO.Notifications;
using VL.Skia;
using YogaSharp;

namespace VL.Fu
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit, HasStateOutput = true)]
    public abstract class FuForwardNodeBase : IFuNode
    {
        protected IFuNode? Input;

        [Fragment]
        public void SetInput(IFuNode? input)
        {
            Input = input;
        }

        [Fragment]
        public FuForwardNodeBase() { }

        public IEnumerable<ITreeNode> Children => Input?.Children ?? [];

        public virtual ITreeNode? Parent
        {
            get => Input?.Parent;
            set
            {
                if (Input != null)
                    Input.Parent = value;
                else
                    return;
            }
        }

        public virtual RectangleF? Bounds => Input?.Bounds;

        public virtual IReadOnlyList<IFuBehaviour> Behaviours => Input?.Behaviours ?? [];

        public virtual int InstanceId => Input?.InstanceId ?? 0;

        public unsafe YGNode* Handle => Input?.Handle;

        public virtual RectangleF Layout => Input?.Layout ?? RectangleF.Empty;

        public virtual void ApplyLayout(LayoutableCalculateLayoutArgs args, RectangleF? ownerLayout)
        {
            Input?.ApplyLayout(args, ownerLayout);
        }

        public virtual bool HitTest(FuPointer pointer)
        {
            return Input?.HitTest(pointer) ?? false;
        }

        public virtual bool IsContainedIn(ISelectionShape shape)
        {
            return Input?.IsContainedIn(shape) ?? false;
        }

        public virtual bool Notify(INotification notification, CallerInfo caller)
        {
            return Input?.Notify(notification, caller) ?? false;
        }

        public virtual void Render(CallerInfo caller)
        {
            Input?.Render(caller);
        }

        public virtual void SetChildren(IEnumerable<ITreeNode> newChildren)
        {
            Input?.SetChildren(newChildren);
        }

        public IEnumerator<ITreeNode> GetEnumerator()
        {
            return Children.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    [ProcessNode(
        Name = "Precompose",
        Category = "Fu",
        FragmentSelection = FragmentSelection.Explicit,
        HasStateOutput = true
    )]
    public class FuPrecomposeNode : FuForwardNodeBase
    {
        private object? _paint;

        [Fragment]
        public FuPrecomposeNode() { }

        [Fragment]
        public void SetPaint(object? paint)
        {
            _paint = paint;
        }

        public override void Render(CallerInfo caller)
        {
            if (_paint is null)
            {
                // Fallback to normal rendering if no paint is set
                Input?.Render(caller);
                foreach (var child in Children ?? [])
                {
                    if (child is ILayer layer)
                        layer.Render(caller);
                }
                return;
            }

            // 1. Evaluate the paint safely.
            // If an upstream node injected a hack, apply it. Otherwise, use _paint as-is.
            object evaluatedPaint =
                caller.RenderInfoHack != null ? caller.RenderInfoHack.Invoke(_paint) : _paint;

            // 2. Extract the SKPaint for the Canvas layer
            var skPaint = evaluatedPaint.FromSkiaPaint();

            // 3. Save the layer with the evaluated paint
            if (skPaint != null)
            {
                // SaveLayer applies the SKPaint (e.g., Opacity, Blur, BlendMode) to everything drawn between now and Restore()
                caller.Canvas.SaveLayer(skPaint);
            }
            else
            {
                caller.Canvas.Save();
            }

            // 4. Render children using the ORIGINAL caller so they retain their individual fill/stroke colors
            Input?.Render(caller);

            foreach (var child in Children ?? [])
            {
                if (child is ILayer layer)
                    layer.Render(caller);
            }

            // 5. Restore the canvas state, compositing the layer to the screen
            caller.Canvas.Restore();
        }
    }
}
