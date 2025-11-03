using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Helpers;
using VL.Fu.Helpers;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu
{
    [ProcessNode(
        Name = "FuNode",
        HasStateOutput = true,
        FragmentSelection = FragmentSelection.Explicit
    )]
    public partial class FuNode : TreeNode<IFuNode>, IFuNode
    {
        protected readonly CachedProperty<FuNodeState> _state = new(new());
        public FuNodeState State
        {
            get => _state.Value;
            set => _state.TrySetValue(value);
        }

        public void SetState(FuNodeState state) => _state.TrySetValue(state);

        protected ILayer _layer;

        public Spread<IFuBehaviour> Behaviours => _behaviours.Value;

        protected readonly CachedProperty<Spread<IFuBehaviour>> _behaviours = new(
            Spread<IFuBehaviour>.Empty
        );

        [Fragment]
        public void SetBehaviours(Spread<IFuBehaviour> behaviors) =>
            _behaviours.TrySetValue(behaviors);

        [Fragment]
        public void SetLayer(ILayer layer) => _layer = layer;

        public RectangleF? Bounds => _layer.Bounds;
        public new Spread<IFuNode> Children => _children.Value;

        protected readonly CachedProperty<Spread<IFuNode>> _children = new(Spread<IFuNode>.Empty);

        protected readonly CachedProperty<int> _callerHash = new(Common.DefaultCallerHash);

        [Fragment]
        public FuNode() { }

        [Fragment(Order = Common.PinOrder.Main)]
        public virtual void SetChildren(
            [Pin(PinGroupKind = Model.PinGroupKind.Collection, PinGroupDefaultCount = 1)]
                Spread<IFuNode> children
        ) => _children.TrySetValue(children, (oc, nc) => base.SetChildren(nc));

        public void Render(CallerInfo caller)
        {
            _callerHash.TrySetValue(caller.GetHashCode());

            _layer?.Render(caller);
            for (int i = 0; i < Children.Count; i++)
                Children[i]?.Render(caller);
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            _layer?.Notify(notification, caller);
            for (int i = 0; i < Children.Count; i++)
                Children[i]?.Notify(notification, caller);
            return false;
        }

        // TODO: Statefull Delegate
        protected readonly CachedProperty<Func<IFuNode, FuCursor, bool>> _hitTestFunction = new(
            HitTestHelper.HitTestBounds
        );

        [Fragment]
        public void SetHitTestFunction(Func<IFuNode, FuCursor, bool> hitTestFunction) =>
            _hitTestFunction.TrySetValue(
                hitTestFunction,
                (p, n) =>
                {
                    Console.WriteLine("Called");
                }
            );

        public bool HitTest(FuCursor cursor) => _hitTestFunction.Value(this, cursor);
    }

    [ProcessNode(
        Name = "FuNode (Spectral)",
        HasStateOutput = true,
        FragmentSelection = FragmentSelection.Explicit
    )]
    public class FuNodeSpectral : FuNode
    {
        [Fragment(Order = Common.PinOrder.Main)]
        public override void SetChildren(Spread<IFuNode> children)
        {
            base.SetChildren(children);
        }
    }
}
