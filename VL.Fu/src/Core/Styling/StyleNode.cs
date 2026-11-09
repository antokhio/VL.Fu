using VL.Fu.Core.Base;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Model;

namespace VL.Fu.Core.Styling
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
        private Spread<IStyle> _input = Spread<IStyle>.Empty;
        private IStyle? _output;

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
            [Pin(PinGroupKind = PinGroupKind.Collection, PinGroupDefaultCount = 1)] Spread<IStyle> input
        )
        {
            input ??= Spread<IStyle>.Empty;
            if (HaveSameItems(_input, input))
                return;

            _input = input;
            RebuildStyle();
        }

        public IStyle? Output => _output;

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
        protected abstract IStyle? BuildStyle(IReadOnlyList<IStyle> inputs);

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

        /// <summary>
        /// Returns <paramref name="value"/>, or an empty optional when it is negative, NaN or infinity, which is reported on this node.
        /// For values Yoga has no meaning for below zero, such as flex grow, padding or border.
        /// </summary>
        protected Optional<float> ValidateNonNegative(Optional<float> value, string name)
        {
            value = ValidateFinite(value, name);
            if (!value.HasValue || value.Value >= 0)
                return value;

            this.LogWarning($"{name} can't be negative, got {value.Value}. It is ignored.");
            return default;
        }

        /// <summary>
        /// Returns <paramref name="value"/>, or an empty optional when it is no member of <typeparamref name="TEnum"/>
        /// or <paramref name="isSupported"/> rejects it, which is reported on this node.
        /// </summary>
        /// <remarks>
        /// VL can hand over any number as an enum, so a value is checked before it reaches Yoga.
        /// <paramref name="isSupported"/> narrows a shared Yoga enum to the members one slot accepts,
        /// e.g. <c>Auto</c> is an align-self value but no align-items value.
        /// </remarks>
        protected Optional<TEnum> ValidateEnum<TEnum>(Optional<TEnum> value, string name, Func<TEnum, bool>? isSupported = null)
            where TEnum : struct, Enum
        {
            if (!value.HasValue || (Enum.IsDefined(value.Value) && (isSupported is null || isSupported(value.Value))))
                return value;

            this.LogWarning($"{name} can't be {value.Value}. It is ignored.");
            return default;
        }

        /// <summary>
        /// Returns <paramref name="value"/>, or an empty optional when a component holds NaN or infinity, which is reported on this node.
        /// The whole vector is ignored, so a style never sets only one axis by accident.
        /// </summary>
        protected Optional<Vector2> ValidateFinite(Optional<Vector2> value, string name)
        {
            if (!value.HasValue || (float.IsFinite(value.Value.X) && float.IsFinite(value.Value.Y)))
                return value;

            this.LogWarning($"{name} must have finite components, got {value.Value}. It is ignored.");
            return default;
        }

        /// <summary>
        /// Returns <paramref name="value"/>, or an empty optional when an element holds NaN or infinity, which is reported on this node.
        /// The whole matrix is ignored, since one bad element would spread NaN into the node's transformation and bounds.
        /// </summary>
        /// <remarks>
        /// Only finiteness is checked. A singular matrix, such as a zero scale, is a valid way to collapse a node.
        /// </remarks>
        protected Optional<Matrix> ValidateFinite(Optional<Matrix> value, string name)
        {
            if (!value.HasValue || IsFinite(value.Value))
                return value;

            this.LogWarning($"{name} must have finite elements, got {value.Value}. It is ignored.");
            return default;
        }

        private static bool IsFinite(in Matrix m) =>
            float.IsFinite(m.M11) && float.IsFinite(m.M12) && float.IsFinite(m.M13) && float.IsFinite(m.M14)
            && float.IsFinite(m.M21) && float.IsFinite(m.M22) && float.IsFinite(m.M23) && float.IsFinite(m.M24)
            && float.IsFinite(m.M31) && float.IsFinite(m.M32) && float.IsFinite(m.M33) && float.IsFinite(m.M34)
            && float.IsFinite(m.M41) && float.IsFinite(m.M42) && float.IsFinite(m.M43) && float.IsFinite(m.M44);

        // A changed item always arrives as a new spread, but a new spread may still hold the same items.
        private static bool HaveSameItems(Spread<IStyle> a, Spread<IStyle> b)
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

        private static Spread<IStyle> WithoutNulls(Spread<IStyle> input)
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
