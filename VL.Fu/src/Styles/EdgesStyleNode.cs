using VL.Fu.Core.Base;
using VL.Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using YogaSharp;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Shared state of the padding, margin and border setters, rebuilt once per change.
    /// A setter either writes one Yoga edge (<see cref="SetEdgeCore"/>) or the four physical edges (<see cref="SetEdgesCore"/>).
    /// An edge without a value is not set here, so an earlier setter or the default holds for it;
    /// without any value the node passes its inputs through.
    /// </summary>
    [ProcessNode]
    public abstract class EdgesStyleNode : StyleNode
    {
        private Optional<float> _value;
        private YGEdge _edge = YGEdge.All;
        private Optional<float> _left;
        private Optional<float> _right;
        private Optional<float> _top;
        private Optional<float> _bottom;

        protected EdgesStyleNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        /// <summary>
        /// Whether an edge may be negative. Only margin may; Yoga treats negative padding and border as 0,
        /// so they are reported and ignored instead.
        /// </summary>
        protected virtual bool AllowsNegative => false;

        /// <summary>
        /// Returns <paramref name="value"/>, or an empty optional when it is not finite or, unless <see cref="AllowsNegative"/>, negative.
        /// The rejected value is reported on this node under <paramref name="name"/>.
        /// </summary>
        protected Optional<float> ValidateEdge(Optional<float> value, string name) =>
            AllowsNegative ? ValidateFinite(value, name) : ValidateNonNegative(value, name);

        /// <summary>
        /// Stores the already validated <paramref name="value"/> for <paramref name="edge"/> and rebuilds the output if either changed.
        /// An unknown edge is reported and the value ignored.
        /// </summary>
        protected void SetEdgeCore(Optional<float> value, YGEdge edge)
        {
            if (!Enum.IsDefined(edge))
            {
                this.LogWarning($"Edge can't be {edge}. It is ignored.");
                value = default;
                edge = _edge;
            }

            var changed = SetValue(ref _value, value);
            changed |= SetValue(ref _edge, edge);
            if (changed)
                RebuildStyle();
        }

        /// <summary>
        /// Stores the already validated physical edges and rebuilds the output if any of them changed.
        /// </summary>
        protected void SetEdgesCore(Optional<float> left, Optional<float> right, Optional<float> top, Optional<float> bottom)
        {
            var changed = SetValue(ref _left, left);
            changed |= SetValue(ref _right, right);
            changed |= SetValue(ref _top, top);
            changed |= SetValue(ref _bottom, bottom);
            if (changed)
                RebuildStyle();
        }

        /// <summary>
        /// Builds the record writing <paramref name="value"/> to <paramref name="edge"/>.
        /// </summary>
        protected abstract IStyle BuildEdgeStyle(IReadOnlyList<IStyle> inputs, YGEdge edge, float value);

        /// <summary>
        /// Builds the record for the physical edges, NaN marking an edge that is not set here.
        /// Only called when at least one edge has a value.
        /// </summary>
        protected abstract IStyle BuildEdgesStyle(IReadOnlyList<IStyle> inputs, float left, float right, float top, float bottom);

        protected sealed override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs)
        {
            if (_left.HasValue || _right.HasValue || _top.HasValue || _bottom.HasValue)
                return BuildEdgesStyle(inputs, OrNaN(_left), OrNaN(_right), OrNaN(_top), OrNaN(_bottom));

            return _value.HasValue ? BuildEdgeStyle(inputs, _edge, _value.Value) : null;
        }

        private static float OrNaN(Optional<float> value) => value.HasValue ? value.Value : float.NaN;
    }
}
