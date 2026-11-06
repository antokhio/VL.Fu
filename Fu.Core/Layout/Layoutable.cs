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
    }

    public abstract class Layoutable<TSelf> : Stylable<TSelf>, ILayoutable
        where TSelf : Layoutable<TSelf>
    {
        private unsafe YGNode* _handle = YGNode.New();
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

        protected Layoutable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

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
        /// then frees only this node's Yoga node. Children keep theirs, since they are disposed by their own owners.
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
            }
        }
    }
}
