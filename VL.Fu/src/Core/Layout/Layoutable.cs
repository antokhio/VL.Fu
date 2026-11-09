using VL.Fu.Core.Extensions;
using VL.Fu.Core.Styling;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
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
        /// Moves, rotates or scales this node around its top-left corner without affecting the layout,
        /// so siblings keep their places. Only X, Y and perspective are used; Z is ignored.
        /// </summary>
        Matrix Transformation { get; }
        void SetTransformation(Matrix transformation);

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
        /// </summary>
        void CalculateLayout(
            float availableWidth = float.NaN,
            float availableHeight = float.NaN,
            YGDirection direction = YGDirection.LTR
        );
    }

    public abstract class Layoutable<TSelf> : Stylable<TSelf>, ILayoutable
        where TSelf : Layoutable<TSelf>
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

        private Matrix _transformation = Matrix.Identity;
        public Matrix Transformation => _transformation;

        public void SetTransformation(Matrix transformation)
        {
            _transformation = transformation;
            UpdateLocalTransformation();
        }

        private Matrix _styledTransformation = Matrix.Identity;
        private Matrix _committedTransformation = Matrix.Identity;

        private SKMatrix _localTransformation = SKMatrix.Identity;

        /// <summary>
        /// Maps this node's own space, with (0, 0) at its top-left corner, into its parent's space:
        /// first <see cref="Transformation"/>, then the move to the <see cref="Layout"/> position.
        /// Kept up to date whenever either of them changes, so reading it is free.
        /// </summary>
        protected SKMatrix LocalTransformation => _localTransformation;

        public YogaConfig? Config => _config;

        /// <summary>
        /// The config this node actually lays out with: its own <see cref="Config"/>,
        /// otherwise its parent's effective config, otherwise <see cref="YogaConfig.Default"/>.
        /// </summary>
        public YogaConfig EffectiveConfig => _effectiveConfig;

        public void SetConfig(YogaConfig? config)
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
        /// Starts the Yoga stage from the default style and the styled transformation from identity.
        /// </summary>
        protected override void BeginStyle()
        {
            base.BeginStyle();
            YogaStyleStage.Reset();
            _styledTransformation = Matrix.Identity;
        }

        /// <summary>
        /// Writes Yoga instructions into the stage, which is copied to this node once at the end of the pass.
        /// </summary>
        protected override unsafe void BuildStyleCore(Style instruction)
        {
            if (instruction is YogaStyle yoga)
                yoga.Build(YogaStyleStage.Node);
            else
                base.BuildStyleCore(instruction);
        }

        /// <summary>
        /// Copies the staged Yoga style, which Yoga does and marks the node dirty for only if it differs,
        /// and commits the styled transformation if it changed since the last pass.
        /// </summary>
        protected override unsafe void EndStyle()
        {
            if (_handle is not null)
                _handle->CopyStyle(YogaStyleStage.Node);

            if (_styledTransformation != _committedTransformation)
            {
                _committedTransformation = _styledTransformation;
                SetTransformation(_committedTransformation);
            }

            base.EndStyle();
        }

        public unsafe void CalculateLayout(
            float availableWidth = float.NaN,
            float availableHeight = float.NaN,
            YGDirection direction = YGDirection.LTR
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
        /// unless it has one of its own.
        /// </summary>
        protected override void OnParentChanged(TSelf? oldParent, TSelf? newParent)
        {
            base.OnParentChanged(oldParent, newParent);
            UpdateConfig();
        }

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
                    handles[i] = newChildren[i]._handle;

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
                var child = children[i];
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

        private void UpdateLocalTransformation()
        {
            _localTransformation = SKMatrix
                .CreateTranslation(_layout.X, _layout.Y)
                .PreConcat(_transformation.ToSKMatrix());

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

            var next = _config ?? Parent?._effectiveConfig ?? YogaConfig.Default;
            if (next == _effectiveConfig)
                return;

            // Point the node at the new config before releasing the old one, which may free it.
            _handle->SetConfig(YogaConfigCache.Acquire(next));
            YogaConfigCache.Release(_effectiveConfig);
            _effectiveConfig = next;

            foreach (var child in Children)
                child.UpdateConfig();
        }
    }
}
