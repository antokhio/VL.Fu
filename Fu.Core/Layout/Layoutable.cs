using Fu.Core.Style;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Model;
using YogaSharp;

namespace Fu.Core.Layout
{
    public interface ILayoutable
    {
        RectangleF Layout { get; }
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
    }

    public abstract class Layoutable<TSelf> : Stylable<TSelf>, ILayoutable
        where TSelf : Layoutable<TSelf>
    {
        private YogaConfig? _config;
        private YogaConfig _effectiveConfig = YogaConfig.Default;

        private unsafe YGNode* _handle = YGNode.New(YogaConfigCache.Acquire(YogaConfig.Default));
        protected unsafe YGNode* Handle => _handle;

        private RectangleF _layout = RectangleF.Empty;
        public RectangleF Layout
        {
            get => _layout;
            protected set => _layout = value;
        }

        private Matrix _transformation = Matrix.Identity;
        public Matrix Transformation => _transformation;

        public void SetTransformation(Matrix transformation) => _transformation = transformation;

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
