using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Tree;
using VL.Lib.IO.Notifications;
using VL.Model;
using VL.Skia;
using YogaSharp;

namespace VL.Fu.Core.Rendering
{
    public interface IRenderable : IInteractive, ILayer
    {
        ILayer? Layer { get; }
        void SetLayer(ILayer layer);
    }

    /// <summary>
    /// Last generic layer of the element chain: draws the node as a VL.Skia layer.
    /// <see cref="Region.RegionProvider"/> closes the chain at <see cref="IFuNode"/>, the type every node of a Fu tree has.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Renderable<TSelf> : Interactive<TSelf>, IRenderable, INotificationRoot
        where TSelf : class, ITraversable<TSelf>, IRenderable
    {
        private NotificationDispatcher? _dispatcher;
        private RectangleF? _explicitBounds;
        private RectangleF? _computedBounds;
        private RectangleF? _viewportArea;

        /// <summary>
        /// The node's rectangle in its parent's space, or in the caller's space for a root, like every VL.Skia layer.
        /// Bounds set with <see cref="SetBounds"/> are returned as they are, like VL.Skia's SetRender and InViewport do.
        /// Otherwise they are computed from the layout and follow <see cref="VL.Fu.Core.Layout.ILayoutable.Transformation"/>,
        /// so a rotated node reports the box around it; null until the node was laid out, since its place is unknown before that.
        /// </summary>
        public RectangleF? Bounds => _explicitBounds ?? _computedBounds;

        /// <summary>
        /// Declares the node's bounds, in the caller's space for a root. A root lays its tree out into them:
        /// their size is the available size and the root is placed at their top-left corner, like content inside InViewport.
        /// Null computes the bounds from the layout again, and a root lays out into the viewport it was last rendered in.
        /// </summary>
        /// <remarks>
        /// On a child they are only reported as <see cref="Bounds"/>, since its parent's layout places it; a warning says so.
        /// Bounds that aren't finite or have a negative size are ignored with a warning.
        /// </remarks>
        public void SetBounds(RectangleF? bounds)
        {
            if (bounds is { } b && !IsValidArea(b))
            {
                this.LogWarning($"Bounds must be finite with a size of at least zero, got {b}. They are ignored.");
                bounds = null;
            }
            else if (bounds is not null && Parent is not null)
            {
                this.LogWarning("Bounds only lay out a root. This node has a parent, which places it; the bounds are only reported.");
            }

            _explicitBounds = bounds;
        }

        private ILayer? _layer;

        public ILayer? Layer => _layer;

        protected Renderable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetLayer(ILayer layer)
        {
            _layer = layer;
        }

        /// <summary>
        /// Runs the notifications this node receives as a root. Created by the first one, and dropped when the node
        /// gets a parent or is disposed, which forgets hover and touch state without sending notifications.
        /// </summary>
        internal NotificationDispatcher Dispatcher => _dispatcher ??= new NotificationDispatcher(this);

        /// <summary>
        /// Draws <see cref="Layer"/> and then the children on top, in order, all in this node's own space,
        /// with (0, 0) at its top-left corner.
        /// </summary>
        /// <remarks>
        /// The local transformation is pushed onto the canvas and onto <see cref="CallerInfo.Transformation"/> alike,
        /// so both stay in sync for layers that read either one; the canvas is restored afterwards.
        /// A root remembers the caller's viewport for its next layout pass (see <see cref="CalculateRootLayout"/>).
        /// A node that was not laid out yet draws nothing, since its place is unknown.
        /// A node with display none draws nothing either, and neither does its subtree, like in the layout.
        /// A node whose overflow is hidden or scroll clips its children to its padding box but not its own layer,
        /// so shadows or outlines it draws stay whole. The clip is antialiased only while the node is rotated or skewed
        /// on the canvas, so axis-aligned clips stay pixel-crisp.
        /// </remarks>
        public void Render(CallerInfo caller)
        {
            if (Parent is null)
                CaptureViewportArea(caller);

            if (!HasLayout || !IsDisplayed)
                return;

            var local = LocalTransformation;
            var canvas = caller.Canvas;
            var save = canvas.Save();
            try
            {
                canvas.Concat(ref local);
                var inner = caller.WithTransformation(caller.Transformation.PreConcat(local));

                _layer?.Render(inner);

                var children = OrderedChildren;
                if (children.Count == 0)
                    return;

                if (ClipArea is { } area)
                {
                    if (area.Width <= 0 || area.Height <= 0)
                        return;

                    canvas.ClipRect(
                        SKRect.Create(area.X, area.Y, area.Width, area.Height),
                        SKClipOperation.Intersect,
                        antialias: !IsScaleTranslate(canvas.TotalMatrix)
                    );
                }

                for (var i = 0; i < children.Count; i++)
                    children[i].Render(inner);
            }
            finally
            {
                canvas.RestoreToCount(save);
            }
        }

        /// <summary>
        /// Delivers <paramref name="notification"/> into the tree the way <see cref="Render"/> draws it, so a Fu tree takes part
        /// in VL's notification flow like any layer. Returns true when a receiver processed it.
        /// </summary>
        /// <remarks>
        /// Only the root chooses where a notification goes, so a child passes it on to its root, whose
        /// <see cref="NotificationDispatcher"/> runs it as a pass.
        /// A positional notification goes to the front-most node under its position and bubbles up to its ancestors;
        /// a point outside every node delivers nothing. Device loss reaches every node and is never reported as processed.
        /// Anything else is offered to the whole tree front to back, like a VL group.
        /// A notification sent to the tree while it is delivering another one is queued and runs right after it,
        /// so that call returns false.
        /// </remarks>
        public bool Notify(INotification notification, CallerInfo caller)
        {
            if (Parent is not null)
                return ((TSelf)(object)this).Root().Notify(notification, caller);

            return Dispatcher.Run(notification, caller);
        }

        bool INotificationRoot.Forward(InteractionPass pass) =>
            Forward(pass.Notification, pass.Caller, pass, depth: 0);

        bool INotificationRoot.TryBuildHitPath(Vector2 position, SKMatrix parentToWindow, List<IInteractive> path) =>
            TryBuildHitPath(position, parentToWindow, path);

        /// <summary>
        /// The body every node runs for a notification, mirroring <see cref="Render"/>: route children front to back,
        /// then <see cref="Layer"/>, then every behavior, all in this node's own space.
        /// </summary>
        /// <param name="depth">This node's index on the route's path, or larger below the end of the path.</param>
        /// <remarks>
        /// Input and fan-out skip a node that isn't drawn, together with its subtree; control reaches every node.
        /// For input, a child or the layer returning true ends the node with true, and its behaviors don't see it;
        /// every behavior still runs when another one returned true. Control and fan-out visit everything and return false.
        /// Children get the notification as it came in. The layer and behaviors get it with a <see cref="FuNotificationSender"/>,
        /// so its positions are in this node's space; while they run, this node is the pass's acting node.
        /// The tree may change during a pass: a node that left the pass's root when it is reached is skipped with its subtree,
        /// and one that left while its children ran doesn't run its own receivers.
        /// </remarks>
        private bool Forward(INotification notification, CallerInfo caller, InteractionPass pass, int depth)
        {
            if (!IsAttachedTo(pass.Root))
                return false;

            var isInput = pass.Class == DeliveryClass.Input;
            if (pass.Class != DeliveryClass.Control && (!HasLayout || !IsDisplayed))
                return false;

            var inner = caller.WithTransformation(caller.Transformation.PreConcat(LocalTransformation));

            if (ForwardToChildren(notification, inner, pass, depth) && isInput)
                return true;

            if (!pass.Route.Receives(depth) || !IsAttachedTo(pass.Root))
                return false;

            var layer = _layer;
            var behaviors = Behaviors;
            if (layer is null && behaviors.Count == 0)
                return false;

            var wrapped = notification.WithSender(
                new FuNotificationSender(
                    this,
                    inner.Transformation,
                    caller.Transformation,
                    caller.Scaling,
                    notification,
                    pass.TimeMs
                )
            );

            var previous = pass.Enter(this);
            try
            {
                if (layer is not null && layer.Notify(wrapped, inner) && isInput)
                    return true;

                var processed = false;
                for (var i = 0; i < behaviors.Count; i++)
                    processed |= behaviors[i].Notify(wrapped, inner);

                return isInput && processed;
            }
            finally
            {
                pass.Leave(previous);
            }
        }

        /// <summary>
        /// Forwards to the next node of the path, or to every child, front to back, where the route descends.
        /// Input stops at the first child that returns true, like a VL group.
        /// A child that left this node since the route was built, or while its siblings ran, is skipped.
        /// </summary>
        private bool ForwardToChildren(INotification notification, CallerInfo inner, InteractionPass pass, int depth)
        {
            var route = pass.Route;
            if (route.Next(depth) is { } next)
            {
                return next is Renderable<TSelf> child
                    && ReferenceEquals(child.Parent, this)
                    && child.Forward(notification, inner, pass, depth + 1);
            }

            if (!route.Descends(depth))
                return false;

            var isInput = pass.Class == DeliveryClass.Input;
            var processed = false;
            var children = OrderedChildren;
            for (var i = children.Count - 1; i >= 0; i--)
            {
                if ((object)children[i] is not Renderable<TSelf> child || !ReferenceEquals(child.Parent, this))
                    continue;

                if (child.Forward(notification, inner, pass, depth + 1))
                {
                    processed = true;
                    if (isInput)
                        break;
                }
            }

            return processed;
        }

        /// <summary>
        /// Lays the tree out into the bounds set with <see cref="SetBounds"/>, otherwise into the viewport
        /// the root was last rendered in, so the layout follows the window one frame later, as in ImGui.
        /// Does nothing on a child, or on a root that has neither yet.
        /// </summary>
        protected void CalculateRootLayout()
        {
            if (Parent is not null || (_explicitBounds ?? _viewportArea) is not { } area)
                return;

            CalculateLayout(area, YGDirection.LTR);
        }

        /// <summary>
        /// An attached node forgets the viewport it was rendered in as a root, since its parent's layout places it now,
        /// and the dispatcher it ran notifications with, since its root runs them now.
        /// </summary>
        protected override void OnParentChanged(TSelf? oldParent, TSelf? newParent)
        {
            base.OnParentChanged(oldParent, newParent);

            if (newParent is not null)
            {
                _viewportArea = null;
                _dispatcher = null;
            }
        }

        /// <summary>
        /// Drops the dispatcher, forgetting hover and touch state without sending notifications.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _dispatcher = null;

            base.Dispose(disposing);
        }

        protected override void OnLocalTransformationChanged()
        {
            base.OnLocalTransformationChanged();

            if (!HasLayout)
                return;

            var layout = Layout;
            _computedBounds = LocalTransformation
                .MapRect(SKRect.Create(layout.Width, layout.Height))
                .ToRectangleF_();
        }

        /// <summary>
        /// Maps the viewport, which is in window pixels, back into the caller's space.
        /// A caller transformation that can't be inverted keeps the last area instead of producing NaN.
        /// </summary>
        private void CaptureViewportArea(CallerInfo caller)
        {
            if (!caller.Transformation.TryInvert(out var inverse))
                return;

            var area = inverse.MapRect(caller.ViewportBounds).ToRectangleF_();
            if (IsValidArea(area))
                _viewportArea = area;
        }

        private bool IsAttachedTo(IInteractive root)
        {
            for (ITraversable? node = this; node is not null; node = node.Parent)
            {
                if (ReferenceEquals(node, root))
                    return true;
            }

            return false;
        }

        private static bool IsScaleTranslate(SKMatrix m) =>
            m.SkewX == 0 && m.SkewY == 0 && m.Persp0 == 0 && m.Persp1 == 0 && m.Persp2 == 1;

        private static bool IsValidArea(RectangleF area) =>
            float.IsFinite(area.X)
            && float.IsFinite(area.Y)
            && float.IsFinite(area.Width)
            && float.IsFinite(area.Height)
            && area.Width >= 0
            && area.Height >= 0;
    }
}
