using Fu.Core.Base;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Model;

namespace Fu.Core.Styling
{
    /// <summary>
    /// Base of every style node in the patch. Holds its pin values and outputs a cached <see cref="Style"/>,
    /// rebuilt only when an input or a value changes, so an unchanged node outputs the same reference every frame.
    /// </summary>
    /// <remarks>
    /// The output is the node's own instruction with its inputs, or just the inputs when nothing is set on the node,
    /// so an empty value lets an earlier setter or the default hold.
    /// Derived nodes call <see cref="RebuildStyle"/> at the end of their constructor and in setters whose value changed.
    /// Invalid pin values are corrected and reported on the style node, not on the nodes it styles.
    /// The base lives in the core so other packages can add style nodes; the concrete nodes live in <c>VL.Fu.Styles</c>.
    /// </remarks>
    [ProcessNode]
    public abstract class StyleNode : NodeBase
    {
        private Spread<Style> _input = Spread<Style>.Empty;
        private Style? _output;

        protected StyleNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// The styles that run before this node's own instruction, in order. Later inputs win over earlier ones.
        /// Unconnected pins are skipped.
        /// </summary>
        /// <remarks>
        /// A pin group, which VL accepts only as an array or <see cref="Spread{T}"/>; hence no <see cref="IReadOnlyList{T}"/>.
        /// </remarks>
        public void SetInput(
            [Pin(PinGroupKind = PinGroupKind.Collection, PinGroupDefaultCount = 1)] Spread<Style> input
        )
        {
            input ??= Spread<Style>.Empty;
            if (HaveSameItems(_input, input))
                return;

            _input = input;
            RebuildStyle();
        }

        public Style? Output => _output;

        /// <summary>
        /// Stores <paramref name="value"/> in <paramref name="field"/> and tells whether it changed.
        /// </summary>
        protected static bool SetValue<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            return true;
        }

        /// <summary>
        /// Rebuilds <see cref="Output"/> from the inputs and <see cref="BuildStyle"/>.
        /// Call it at the end of the constructor and whenever a value used by <see cref="BuildStyle"/> changed.
        /// </summary>
        protected void RebuildStyle()
        {
            var inputs = WithoutNulls(_input);
            _output =
                BuildStyle(inputs)
                ?? inputs.Count switch
                {
                    0 => null,
                    1 => inputs[0],
                    _ => new CombinedStyle(inputs),
                };
        }

        /// <summary>
        /// Builds this node's own instruction with <paramref name="inputs"/> as its inputs,
        /// or returns null when nothing is set, so the output is the inputs alone.
        /// <paramref name="inputs"/> holds no nulls and is immutable, so a style can keep it without copying.
        /// </summary>
        protected abstract Style? BuildStyle(IReadOnlyList<Style> inputs);

        /// <summary>
        /// Returns <paramref name="value"/>, or an empty optional when it holds NaN or infinity, which is reported on this node.
        /// An empty optional means not set, so the style falls back to earlier setters or the default.
        /// </summary>
        protected Optional<float> ValidateFinite(Optional<float> value, string name)
        {
            if (!value.HasValue || float.IsFinite(value.Value))
                return value;

            this.LogWarning($"{name} must be a finite number, got {value.Value}. It is ignored.");
            return default;
        }

        // A changed item always arrives as a new spread, but a new spread may still hold the same items.
        private static bool HaveSameItems(Spread<Style> a, Spread<Style> b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a.Count != b.Count)
                return false;

            for (var i = 0; i < a.Count; i++)
            {
                if (!ReferenceEquals(a[i], b[i]))
                    return false;
            }

            return true;
        }

        private static Spread<Style> WithoutNulls(Spread<Style> input)
        {
            foreach (var item in input)
            {
                if (item is null)
                    return input.Where(i => i is not null).ToSpread();
            }

            return input;
        }
    }
}
