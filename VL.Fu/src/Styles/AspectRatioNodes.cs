using VL.Fu.Core.Base;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Shared state of the aspect ratio setters: one cached ratio, width divided by height, rebuilt once per change.
    /// Without a value it is not set here, so an earlier setter holds, or both axes are sized on their own.
    /// </summary>
    [ProcessNode]
    public abstract class AspectRatioStyleNode : StyleNode
    {
        private Optional<float> _aspectRatio;

        protected AspectRatioStyleNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        /// <summary>
        /// Stores <paramref name="width"/> divided by <paramref name="height"/> and rebuilds the output if the ratio changed,
        /// so proportional inputs such as 16:9 and 32:18 keep the output.
        /// A width or height of 0 or below, or a ratio out of float range, is reported with <paramref name="value"/> and ignored.
        /// </summary>
        protected void SetAspectRatioCore(float width, float height, object value)
        {
            var ratio = width / height;
            if (!(width > 0 && height > 0 && ratio > 0 && float.IsFinite(ratio)))
            {
                this.LogWarning($"AspectRatio must be above 0, got {value}. It is ignored.");
                ClearAspectRatio();
                return;
            }

            if (SetValue(ref _aspectRatio, ratio))
                RebuildStyle();
        }

        /// <summary>
        /// Drops the ratio, so the node passes its inputs through.
        /// </summary>
        protected void ClearAspectRatio()
        {
            if (SetValue(ref _aspectRatio, default))
                RebuildStyle();
        }

        protected sealed override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _aspectRatio.HasValue ? new AspectRatioStyle(inputs, _aspectRatio.Value) : null;
    }

    /// <summary>
    /// Sets the aspect ratio, width divided by height: when only one axis is known, the other is derived from it.
    /// A value of 0 or below is ignored.
    /// </summary>
    [ProcessNode(Name = "SetAspectRatio")]
    public sealed class SetAspectRatioNode : AspectRatioStyleNode
    {
        public SetAspectRatioNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetAspectRatio(Optional<float> aspectRatio = default)
        {
            aspectRatio = ValidateFinite(aspectRatio, "AspectRatio");
            if (aspectRatio.HasValue)
                SetAspectRatioCore(aspectRatio.Value, 1, aspectRatio.Value);
            else
                ClearAspectRatio();
        }
    }

    /// <summary>
    /// Sets the aspect ratio as a width and height pair, such as 16, 9: when only one axis is known, the other is derived from it.
    /// A component of 0 or below is ignored.
    /// </summary>
    [ProcessNode(Name = "SetAspectRatio (Vector2)")]
    public sealed class SetAspectRatioVector2Node : AspectRatioStyleNode
    {
        public SetAspectRatioVector2Node([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetAspectRatioVector2(Optional<Vector2> aspectRatio = default)
        {
            aspectRatio = ValidateFinite(aspectRatio, "AspectRatio");
            if (aspectRatio.HasValue)
                SetAspectRatioCore(aspectRatio.Value.X, aspectRatio.Value.Y, aspectRatio.Value);
            else
                ClearAspectRatio();
        }
    }

    /// <summary>
    /// Sets the aspect ratio as a whole-number width and height pair, such as 16, 9: when only one axis is known, the other is derived from it.
    /// A component of 0 or below is ignored.
    /// </summary>
    [ProcessNode(Name = "SetAspectRatio (Int2)")]
    public sealed class SetAspectRatioInt2Node : AspectRatioStyleNode
    {
        public SetAspectRatioInt2Node([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public void SetAspectRatioInt2(Optional<Int2> aspectRatio = default)
        {
            if (aspectRatio.HasValue)
                SetAspectRatioCore(aspectRatio.Value.X, aspectRatio.Value.Y, aspectRatio.Value);
            else
                ClearAspectRatio();
        }
    }
}
