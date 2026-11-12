using Microsoft.Extensions.Logging;
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Extensions;
using VL.Fu.Core.Styling;
using VL.Fu.Core.Tree;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Core.Layout
{
    public interface ILayoutable : IStylable
    {
        /// <summary>
        /// The rectangle Yoga computed for this node, relative to its parent's top-left corner.
        /// It is empty until the root of the tree ran <see cref="CalculateLayout"/>.
        /// </summary>
        RectangleF Layout { get; }

        /// <summary>
        /// Moves, rotates or scales this node around <see cref="TransformOrigin"/>, its center by default, without affecting the layout,
        /// so siblings keep their places. Only X, Y and perspective are used; Z is ignored.
        /// The origin follows the layout, so a node that changes size keeps turning around the same relative point.
        /// </summary>
        Matrix Transformation { get; }
        void SetTransformation(Matrix transformation);

        /// <summary>
        /// The point <see cref="Transformation"/> rotates and scales around, relative to the node's box:
        /// (0, 0) is its center, (-1, -1) its top-left and (1, 1) its bottom-right corner.
        /// </summary>
        /// <remarks>
        /// Values beyond -1 and 1 put the origin outside the box. A value that isn't finite is ignored with a warning.
        /// </remarks>
        Vector2 TransformOrigin { get; }
        void SetTransformOrigin(Vector2 origin);

        /// <summary>
        /// This node's own Yoga config, or null when it inherits the one of its parent
        /// (<see cref="YogaConfig.Default"/> at the root).
        /// </summary>
        YogaConfig? Config { get; }

        /// <summary>
        /// Sets the Yoga config for this node and every descendant that has no config of its own.
        /// Pass null to inherit again.
        /// </summary>
        void SetConfig(YogaConfig? config);

        /// <summary>
        /// Lays out the whole tree below this node and updates <see cref="Layout"/> on every node that changed.
        /// Only a root can do this; on a node with a parent it logs a warning and does nothing.
        /// A NaN size leaves that axis unconstrained, so the root takes the size of its content.
        /// The root is placed at (0, 0) of its caller's space.
        /// </summary>
        void CalculateLayout(
            float availableWidth = float.NaN,
            float availableHeight = float.NaN,
            YGDirection direction = YGDirection.LTR
        );
    }

    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Layoutable<TSelf> : Stylable<TSelf>, ILayoutable
        where TSelf : class, ITraversable<TSelf>, ILayoutable
    {
        private YogaConfig? _config;
        private YogaConfig _effectiveConfig = YogaConfig.Default;

        private unsafe YGNode* _handle = YGNode.New(YogaConfigCache.Acquire(YogaConfig.Default));
        protected unsafe YGNode* Handle => _handle;

        private RectangleF _layout = RectangleF.Empty;
        public RectangleF Layout => _layout;

        private bool _hasLayout;

        /// <summary>
        /// Whether the root of this node's tree laid it out at least once.
        /// Until then <see cref="Layout"/> is empty and says nothing about where the node is.
        /// </summary>
        protected bool HasLayout => _hasLayout;

        /// <summary>
        /// False when the node's style sets display to none, which removes it and its subtree from the layout.
        /// Read from the style committed by the last styling pass, so it is cheap to check every frame.
        /// </summary>
        protected unsafe bool IsDisplayed => _handle is not null && _handle->GetDisplay() != YGDisplay.None;

        /// <summary>
        /// The area this node's children are clipped to, in its own space: its padding box, inside the border.
        /// Null when the node was not laid out yet or its overflow is visible, since then nothing is clipped.
        /// </summary>
        /// <remarks>
        /// Read from the committed style and the last layout on every access, not cached, so changing only the overflow
        /// takes effect without a new layout. A border wider than the box leaves an empty area.
        /// </remarks>
        protected unsafe RectangleF? ClipArea
        {
            get
            {
                if (!_hasLayout || _handle is null || _handle->GetOverflow() == YGOverflow.Visible)
                    return null;

                var left = _handle->GetComputedBorder(YGEdge.Left);
                var top = _handle->GetComputedBorder(YGEdge.Top);
                var right = Math.Max(left, _layout.Width - _handle->GetComputedBorder(YGEdge.Right));
                var bottom = Math.Max(top, _layout.Height - _handle->GetComputedBorder(YGEdge.Bottom));
                return new RectangleF(left, top, right - left, bottom - top);
            }
        }

        private Matrix _transformation = Matrix.Identity;
        public Matrix Transformation => _transformation;

        [Fragment(Order = PinOrder.Transform)]
        public void SetTransformation(Matrix transformation)
        {
            _transformation = transformation;
            UpdateLocalTransformation();
        }

        private Matrix _styledTransformation = Matrix.Identity;
        private Matrix _committedTransformation = Matrix.Identity;

        private Vector2 _transformOrigin;
        public Vector2 TransformOrigin => _transformOrigin;

        public void SetTransformOrigin(Vector2 origin)
        {
            if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y))
            {
                Log(LogLevel.Warning, $"TransformOrigin must be finite, got {origin}. It is ignored.");
                return;
            }

            if (origin == _transformOrigin)
                return;

            _transformOrigin = origin;
            UpdateLocalTransformation();
        }

        private Vector2 _styledTransformOrigin;
        private Vector2 _committedTransformOrigin;

        private SKMatrix _localTransformation = SKMatrix.Identity;

        // The root's place in its caller's space, set by each layout pass on the root; zero for every child.
        private Vector2 _origin;

        /// <summary>
        /// Maps this node's own space, with (0, 0) at its top-left corner, into its parent's space:
        /// first <see cref="Transformation"/> around <see cref="TransformOrigin"/> in the node's box, then the move to the <see cref="Layout"/> position.
        /// A root also moves to the top-left of the area it was laid out into, so it maps into its caller's space.
        /// Kept up to date whenever any of them changes, so reading it is free.
        /// </summary>
        protected SKMatrix LocalTransformation => _localTransformation;

        public YogaConfig? Config => _config;

        /// <summary>
        /// The config this node actually lays out with: its own <see cref="Config"/>,
        /// otherwise its parent's effective config, otherwise <see cref="YogaConfig.Default"/>.
        /// </summary>
        public YogaConfig EffectiveConfig => _effectiveConfig;

        [Fragment(Order = PinOrder.Debug)]
        public void SetConfig([Pin(Visibility = PinVisibility.Optional)] YogaConfig? config)
        {
            if (Equals(_config, config))
                return;

            _config = config;
            UpdateConfig();
        }

        protected Layoutable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// Sets the transformation the running styling pass commits to <see cref="Transformation"/>.
        /// Each pass starts at identity, and the commit writes only when the styled value changed,
        /// so a directly set transformation is kept until the style changes it.
        /// </summary>
        internal void StageTransformation(Matrix transformation) =>
            _styledTransformation = transformation;

        /// <summary>
        /// Sets the origin the running styling pass commits to <see cref="TransformOrigin"/>.
        /// Each pass starts at the center, and like the transformation the commit writes only when the styled value changed.
        /// </summary>
        internal void StageTransformOrigin(Vector2 origin) =>
            _styledTransformOrigin = origin;

        /// <summary>
        /// Starts the Yoga stage from the default style, the styled transformation from identity and its origin from the center.
        /// </summary>
        protected override void BeginStyle()
        {
            base.BeginStyle();
            YogaStyleStage.Reset();
            _styledTransformation = Matrix.Identity;
            _styledTransformOrigin = Vector2.Zero;
        }

        /// <summary>
        /// Writes Yoga instructions into the stage, which is copied to this node once at the end of the pass.
        /// </summary>
        protected override unsafe void BuildStyleCore(IStyle instruction)
        {
            if (instruction is YogaStyle yoga)
                yoga.Build(YogaStyleStage.Node);
            else
                base.BuildStyleCore(instruction);
        }

        /// <summary>
        /// Copies the staged Yoga style, which Yoga does and marks the node dirty for only if it differs,
        /// and commits the styled transformation and its origin if they changed since the last pass.
        /// </summary>
        protected override unsafe void EndStyle()
        {
            if (_handle is not null)
                _handle->CopyStyle(YogaStyleStage.Node);

            if (_styledTransformOrigin != _committedTransformOrigin)
            {
                _committedTransformOrigin = _styledTransformOrigin;
                SetTransformOrigin(_committedTransformOrigin);
            }

            if (_styledTransformation != _committedTransformation)
            {
                _committedTransformation = _styledTransformation;
                SetTransformation(_committedTransformation);
            }

            base.EndStyle();
        }

        public void CalculateLayout(
            float availableWidth = float.NaN,
            float availableHeight = float.NaN,
            YGDirection direction = YGDirection.LTR
        ) => CalculateLayout(Vector2.Zero, availableWidth, availableHeight, direction);

        /// <summary>
        /// Lays out the whole tree into <paramref name="area"/>, a rectangle in the caller's space:
        /// its size is the available size, and the root is placed at its top-left corner.
        /// Only a root can do this; on a node with a parent it logs a warning and does nothing.
        /// </summary>
        protected void CalculateLayout(RectangleF area, YGDirection direction) =>
            CalculateLayout(new Vector2(area.X, area.Y), area.Width, area.Height, direction);

        private unsafe void CalculateLayout(
            Vector2 origin,
            float availableWidth,
            float availableHeight,
            YGDirection direction
        )
        {
            if (_handle is null)
                return;

            if (Parent is not null)
            {
                // Yoga would lay this subtree out as if it were a root and overwrite the results of the real root.
                Log(
                    LogLevel.Warning,
                    "Only a root element can calculate layout. This element has a parent, so its root calculates it."
                );
                return;
            }

            SetOrigin(origin);

            _handle->CalculateLayout(availableWidth, availableHeight, direction);

            if (_handle->GetHasNewLayout())
                ApplyLayout();
        }

        /// <summary>
        /// Called after <see cref="Layout"/> changed, during <see cref="CalculateLayout"/> on the root.
        /// Parents are updated before their children, so a child sees its parent's new layout already.
        /// <see cref="LocalTransformation"/> already includes the new position.
        /// </summary>
        protected virtual void OnLayoutChanged(RectangleF oldLayout, RectangleF newLayout) { }

        /// <summary>
        /// Called after <see cref="LocalTransformation"/> was rebuilt: when <see cref="Transformation"/> was set,
        /// when <see cref="Layout"/> changed, and once when the node is laid out for the first time.
        /// The place to update anything derived from it, so it is not recomputed every frame.
        /// </summary>
        protected virtual void OnLocalTransformationChanged() { }

        /// <summary>
        /// A node that was attached, moved or detached picks up the config of its new surroundings,
        /// unless it has one of its own. An attached node drops its place as a root, since its parent's layout places it now.
        /// </summary>
        protected override void OnParentChanged(TSelf? oldParent, TSelf? newParent)
        {
            base.OnParentChanged(oldParent, newParent);

            if (newParent is not null)
                SetOrigin(Vector2.Zero);

            UpdateConfig();
        }

        /// <summary>
        /// Only layoutable nodes can be children, since every child is mirrored into the Yoga tree.
        /// </summary>
        protected override bool AcceptsChild(TSelf child) =>
            base.AcceptsChild(child) && child is Layoutable<TSelf>;

        /// <summary>
        /// Mirrors <see cref="Tree.Traversable{TSelf}.Children"/> into the Yoga tree.
        /// Yoga makes this node the owner of every new child and clears the owner of removed ones.
        /// A child that moved here from another parent has already been removed there,
        /// because the old parent receives its own <see cref="OnChildrenChanged"/> first.
        /// </summary>
        protected override void OnChildrenChanged(
            IReadOnlyList<TSelf> oldChildren,
            IReadOnlyList<TSelf> newChildren
        )
        {
            base.OnChildrenChanged(oldChildren, newChildren);

            unsafe
            {
                if (_handle is null)
                    return;

                var handles = new YGNode*[newChildren.Count];
                for (var i = 0; i < handles.Length; i++)
                    handles[i] = AsLayoutable(newChildren[i])._handle;

                _handle->SetChildren(handles);
            }
        }

        /// <summary>
        /// Detaches from the tree first (see <see cref="Tree.Traversable{TSelf}"/>), which also unlinks the Yoga node,
        /// then frees only this node's Yoga node and gives back its config. Children keep theirs, since they are disposed by their own owners.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            unsafe
            {
                if (_handle is null)
                    return;

                _handle->Dispose();
                _handle = null;
                YogaConfigCache.Release(_effectiveConfig);
            }
        }

        /// <summary>
        /// Reads the results of a Yoga pass top-down, visiting only subtrees that Yoga laid out again.
        /// A child Yoga did not flag is still read, since Yoga moves a child that kept its cached size without flagging it.
        /// Its own children are relative to it and therefore unaffected.
        /// </summary>
        private unsafe void ApplyLayout()
        {
            ReadLayout();
            _handle->SetHasNewLayout(false);

            var children = Children;
            for (var i = 0; i < children.Count; i++)
            {
                var child = AsLayoutable(children[i]);
                if (child._handle is null)
                    continue;

                if (child._handle->GetHasNewLayout())
                    child.ApplyLayout();
                else
                    child.ReadLayout();
            }
        }

        private unsafe void ReadLayout()
        {
            var layout = new RectangleF(
                _handle->GetComputedLeft(),
                _handle->GetComputedTop(),
                _handle->GetComputedWidth(),
                _handle->GetComputedHeight()
            );

            var changed = !IsSameLayout(layout, _layout);
            if (!changed && _hasLayout)
                return;

            // The first read also counts when Yoga left the node at its initial empty layout, since it becomes known now.
            var old = _layout;
            _layout = layout;
            _hasLayout = true;
            UpdateLocalTransformation();

            if (changed)
                OnLayoutChanged(old, layout);
        }

        private void SetOrigin(Vector2 origin)
        {
            if (origin.X.Equals(_origin.X) && origin.Y.Equals(_origin.Y))
                return;

            _origin = origin;
            UpdateLocalTransformation();
        }

        /// <summary>
        /// The point <see cref="Transformation"/> turns and scales around, in the parent's space (the caller's for a root),
        /// without the transformation: <see cref="LocalTransformation"/> is a move to it, then <see cref="Transformation"/>,
        /// then the move back by <see cref="TransformOrigin"/>'s point in the node's box.
        /// </summary>
        internal Vector2 TransformPivotInParent => _origin + new Vector2(_layout.X, _layout.Y) + PivotInNode();

        private Vector2 PivotInNode() =>
            new((_transformOrigin.X + 1) / 2 * _layout.Width, (_transformOrigin.Y + 1) / 2 * _layout.Height);

        private void UpdateLocalTransformation()
        {
            var pivot = PivotInNode();
            var pivotInParent = TransformPivotInParent;

            _localTransformation = SKMatrix
                .CreateTranslation(pivotInParent.X, pivotInParent.Y)
                .PreConcat(_transformation.ToSKMatrix())
                .PreConcat(SKMatrix.CreateTranslation(-pivot.X, -pivot.Y));

            OnLocalTransformationChanged();
        }

        /// <summary>
        /// Compares exactly and NaN-safely, without any tolerance.
        /// Small changes still count, so a node never keeps a stale layout.
        /// </summary>
        private static bool IsSameLayout(RectangleF a, RectangleF b) =>
            a.X.Equals(b.X)
            && a.Y.Equals(b.Y)
            && a.Width.Equals(b.Width)
            && a.Height.Equals(b.Height);

        /// <summary>
        /// Switches the Yoga node to the effective config and pushes the change down to the children.
        /// Children with their own config stop the walk for their subtree, since nothing changes for them.
        /// </summary>
        private unsafe void UpdateConfig()
        {
            if (_handle is null)
                return;

            var inherited = Parent is Layoutable<TSelf> parent
                ? parent._effectiveConfig
                : YogaConfig.Default;
            var next = _config ?? inherited;
            if (next == _effectiveConfig)
                return;

            // Point the node at the new config before releasing the old one, which may free it.
            _handle->SetConfig(YogaConfigCache.Acquire(next));
            YogaConfigCache.Release(_effectiveConfig);
            _effectiveConfig = next;

            foreach (var child in Children)
                AsLayoutable(child).UpdateConfig();
        }

        // Safe for every child, since AcceptsChild only lets layoutable nodes in.
        private static Layoutable<TSelf> AsLayoutable(TSelf node) =>
            (Layoutable<TSelf>)(object)node;
    }
}
