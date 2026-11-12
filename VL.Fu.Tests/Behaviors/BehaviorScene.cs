using Stride.Core.Mathematics;
using VL.Core;
using VL.Fu.Core.Interaction;
using VL.Lib.Collections;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Skia;
using YogaSharp;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// A 100 × 100 root with Back at (10, 10) and Front at (30, 30), both 40 × 40, and Child at Front's top-left corner, 10 × 10,
    /// so (35, 35) hits Child, (60, 60) hits Front and (15, 15) hits Back. Nothing is transformed, so window pixels,
    /// the root's space and every parent space only differ by the nodes' positions, and offsets equal window distances.
    /// </summary>
    /// <remarks>
    /// Notifications run through the root's dispatcher like in a patch, so claims are requested, resolved, granted and cancelled for real.
    /// </remarks>
    internal sealed unsafe class BehaviorScene : IDisposable
    {
        internal sealed class Node : Interactive<Node>
        {
            public Node()
                : base(null!) { }

            public YGNode* Native => Handle;
        }

        private static readonly Vector2 Area = new(100, 100);

        public BehaviorScene()
        {
            Root.Native->SetWidth(100);
            Root.Native->SetHeight(100);
            Place(Back, 10, 10, 40);
            Place(Front, 30, 30, 40);
            Place(Child, 0, 0, 10);
            Front.SetChildren(Spread.Create(new[] { Child }));
            Root.SetChildren(Spread.Create(new[] { Back, Front }));
            Root.CalculateLayout();
        }

        public Node Root { get; } = new();
        public Node Back { get; } = new();
        public Node Front { get; } = new();
        public Node Child { get; } = new();

        public PointerClaims Claims => Root.Dispatcher.Claims;

        public void Attach(Node node, params IBehavior[] behaviors) => node.SetBehaviors(Spread.Create(behaviors));

        /// <summary>
        /// Gives <paramref name="node"/> a new size and lays the scene out again.
        /// </summary>
        public void Resize(Node node, float width, float height)
        {
            node.Native->SetWidth(width);
            node.Native->SetHeight(height);
            Root.CalculateLayout();
        }

        /// <summary>
        /// Puts a new node as the only child of <paramref name="parent"/>, at (<paramref name="left"/>, <paramref name="top"/>)
        /// in it, and lays the scene out again. The scene disposes it.
        /// </summary>
        public Node AddChild(Node parent, float left, float top, float width, float height)
        {
            var node = new Node();
            _extra.Add(node);
            Place(node, left, top, width, height);
            parent.SetChildren(Spread.Create(new[] { node }));
            Root.CalculateLayout();
            return node;
        }

        /// <summary>
        /// Hides what overflows <paramref name="node"/> and gives it a border on every edge, then lays the scene out again.
        /// </summary>
        public void Clip(Node node, float border)
        {
            node.Native->SetOverflow(YGOverflow.Hidden);
            node.Native->SetBorder(YGEdge.All, border);
            Root.CalculateLayout();
        }

        public bool Notify(INotification notification) => Root.Notify(notification, CallerInfo.Default);

        public bool Down(float x, float y, MouseButtons button = MouseButtons.Left, Keys modifiers = Keys.None) =>
            Notify(new MouseDownNotification(new Vector2(x, y), Area, button, modifiers, null!));

        public bool Up(float x, float y, MouseButtons button = MouseButtons.Left, Keys modifiers = Keys.None) =>
            Notify(new MouseUpNotification(new Vector2(x, y), Area, button, modifiers, null!));

        public bool Move(float x, float y, Keys modifiers = Keys.None) =>
            Notify(new MouseMoveNotification(new Vector2(x, y), Area, modifiers, null!));

        public bool Wheel(float x, float y, float notches, Keys modifiers = Keys.None) =>
            Notify(new MouseWheelNotification(new Vector2(x, y), Area, (int)(notches * PointerInput.WheelUnitsPerNotch), modifiers, null!));

        public bool HorizontalWheel(float x, float y, float notches) =>
            Notify(
                new MouseHorizontalWheelNotification(new Vector2(x, y), Area, (int)(notches * PointerInput.WheelUnitsPerNotch), Keys.None, null!)
            );

        public bool Escape() => Notify(new KeyDownNotification(Keys.Escape, Keys.None, null!));

        public bool TouchDown(int id, float x, float y) => Touch(TouchNotificationKind.TouchDown, id, x, y);

        public bool TouchMove(int id, float x, float y) => Touch(TouchNotificationKind.TouchMove, id, x, y);

        public bool TouchUp(int id, float x, float y) => Touch(TouchNotificationKind.TouchUp, id, x, y);

        public void Dispose()
        {
            foreach (var node in _extra)
                node.Dispose();

            Child.Dispose();
            Front.Dispose();
            Back.Dispose();
            Root.Dispose();
        }

        // A touch pressed while no other one is down is the primary one until it lifts, as Windows reports it.
        private bool Touch(TouchNotificationKind kind, int id, float x, float y)
        {
            if (kind == TouchNotificationKind.TouchDown && _primaryTouch is null)
                _primaryTouch = id;

            var primary = _primaryTouch == id;
            if (kind == TouchNotificationKind.TouchUp && primary)
                _primaryTouch = null;

            return Notify(new TouchNotification(kind, new Vector2(x, y), Area, id, primary, Vector2.Zero, 0, Keys.None, null!));
        }

        private int? _primaryTouch;

        private readonly List<Node> _extra = new();

        private static void Place(Node node, float left, float top, float size) => Place(node, left, top, size, size);

        private static void Place(Node node, float left, float top, float width, float height)
        {
            node.Native->SetPositionType(YGPositionType.Absolute);
            node.Native->SetPosition(YGEdge.Left, left);
            node.Native->SetPosition(YGEdge.Top, top);
            node.Native->SetWidth(width);
            node.Native->SetHeight(height);
        }
    }

    /// <summary>
    /// A real VL channel and every value written into it since it was created, in order.
    /// Equal values written with <see cref="ChannelHelpers.EnsureValue{T}"/> don't show up twice; fired events do.
    /// </summary>
    internal sealed class Recorder<T> : IDisposable
    {
        private readonly IDisposable _subscription;

        public Recorder(T initial)
        {
            Channel = VL.Lib.Reactive.Channel.Create(initial);
            _subscription = Channel.Subscribe(v => Values.Add(v!));
        }

        public IChannel<T> Channel { get; }

        public List<T> Values { get; } = new();

        public T? Value => Channel.Value;

        public void Dispose()
        {
            _subscription.Dispose();
            Channel.Dispose();
        }
    }

    /// <summary>
    /// A behavior made of one function, for the parts of a scene that only need to request, process or watch.
    /// </summary>
    internal sealed class Probe(Func<Probe, INotification, bool> notify) : IBehavior
    {
        public List<INotification> Received { get; } = new();

        public bool Notify(INotification notification, CallerInfo caller)
        {
            Received.Add(notification);
            return notify(this, notification);
        }
    }
}
