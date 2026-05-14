using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;
using VL.Core;
using VL.Core.Import;
using YogaSharp;

namespace VL.Fu.Core
{
    public interface IMeasurable : ILayoutable
    {
        Func<MeasureFuncArgs, YGSize>? MeasureFunc { get; }
        object? MeasureContext { get; }
    }

    public record struct MeasureFuncArgs(
        object? Context,
        float Width,
        YGMeasureMode WidthMode,
        float Height,
        YGMeasureMode HeightMode
    ) { }

    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Measurable : Stylable, IMeasurable
    {
        private GCHandle _selfHandle;

        [Fragment]
        public Measurable(NodeContext nodeContext)
            : base(nodeContext)
        {
            _selfHandle = GCHandle.Alloc(this, GCHandleType.Weak);

            unsafe
            {
                Handle->SetContext((void*)GCHandle.ToIntPtr(_selfHandle));
            }
        }

        private Func<MeasureFuncArgs, YGSize>? _measureFunc;
        private nint _measureFuncHandle;
        public Func<MeasureFuncArgs, YGSize>? MeasureFunc => _measureFunc;

        [Fragment]
        public void SetMeasureFunc(
            [Pin(Visibility = Model.PinVisibility.Optional)]
                Func<MeasureFuncArgs, YGSize>? measureFunc
        )
        {
            if (ReferenceEquals(_measureFunc, measureFunc))
                return;

            _measureFunc = measureFunc;

            if (_measureFunc != null)
            {
                unsafe
                {
                    _measureFuncHandle = (nint)
                        (delegate* unmanaged[Cdecl]<
                            YGNode*,
                            float,
                            YGMeasureMode,
                            float,
                            YGMeasureMode,
                            YGSize>)
                            &MeasurableExtensions.MeasureFuncAdapter;
                    Handle->SetMeasureFunc(_measureFuncHandle);
                }
            }
            else
            {
                unsafe
                {
                    _measureFuncHandle = 0;
                    Handle->SetMeasureFunc(_measureFuncHandle);
                }
            }
        }

        private object? _measureContext;

        [Fragment]
        public void SetMeasureContext(
            [Pin(Visibility = Model.PinVisibility.Optional)] object? measureContext
        )
        {
            _measureContext = measureContext;
        }

        public object? MeasureContext => _measureContext;

        public override void Dispose()
        {
            unsafe
            {
                if (Handle != null)
                {
                    Handle->SetContext(null);

                    _measureFuncHandle = 0;
                }
            }

            if (_selfHandle.IsAllocated)
                _selfHandle.Free();

            base.Dispose();
        }
    }

    public static class MeasurableExtensions
    {
        /// <summary>
        /// Resolves the managed FlexNode associated with a native YGNode by
        /// reading the weak GCHandle stored in YGNode->GetContext().
        /// Returns null if the node has no managed owner or the owner has
        /// been collected.
        /// </summary>
        private static unsafe IFuNode? ResolveNode(YGNode* node)
        {
            if (node == null)
                return null;
            var ctx = node->GetContext();
            if (ctx == null)
                return null;
            var handle = GCHandle.FromIntPtr((nint)ctx);
            if (!handle.IsAllocated)
                return null;
            return handle.Target as IFuNode;
        }

        /// <summary>
        /// Native delegate adapter, used internally.
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static unsafe YGSize MeasureFuncAdapter(
            YGNode* node,
            float width,
            YGMeasureMode widthMode,
            float height,
            YGMeasureMode heightMode
        )
        {
            var item = ResolveNode(node);

            if (item != null)
            {
                var size = item.MeasureFunc?.Invoke(
                    new MeasureFuncArgs(item.MeasureContext, width, widthMode, height, heightMode)
                );

                if (size != null)
                {
                    return size.Value;
                }
            }

            return new YGSize { Width = float.NaN, Height = float.NaN };
        }

        public static bool HasMeaureFunc(this IMeasurable measurable)
        {
            unsafe
            {
                return measurable.Handle->HasMeasureFunc();
            }
        }
    }

    public record struct TextMeasureContext(string Text, SKPaint Paint);

    public static class TextMeasurableExtensions
    {
        // Note: I added the layout constraints (width, widthMode, height, heightMode)
        // because they are strictly required to perform the text-wrapping calculations.
        public static YGSize MeasureText(
            this object? context,
            float width,
            YGMeasureMode widthMode,
            float height,
            YGMeasureMode heightMode
        )
        {
            // 1. Unbox and validate the context
            if (context is not TextMeasureContext measureContext)
            {
                return new YGSize { Width = 0, Height = 0 };
            }

            string text = measureContext.Text ?? string.Empty;
            SKPaint paint = measureContext.Paint;

            if (string.IsNullOrEmpty(text) || paint == null)
            {
                return new YGSize { Width = 0, Height = 0 };
            }

            // 2. Get font metrics to determine line height
            var metrics = paint.FontMetrics;
            float singleLineHeight = metrics.Descent - metrics.Ascent;

            float measuredWidth = 0;
            float measuredHeight = 0;

            // 3. Calculate Width
            if (widthMode == YGMeasureMode.Exactly)
            {
                measuredWidth = width;
            }
            else if (widthMode == YGMeasureMode.Undefined)
            {
                // Infinite space: measure as a single continuous line
                measuredWidth = paint.MeasureText(text);
            }
            else if (widthMode == YGMeasureMode.AtMost)
            {
                // Constrained space: we must word-wrap
                float maxLineWidth = 0;
                float currentLineWidth = 0;
                float spaceWidth = paint.MeasureText(" ");

                // Simple space-based word wrapping
                string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    float wordWidth = paint.MeasureText(word);

                    // If adding this word exceeds the width limit, wrap to next line
                    if (currentLineWidth + wordWidth > width && currentLineWidth > 0)
                    {
                        maxLineWidth = Math.Max(maxLineWidth, currentLineWidth);
                        currentLineWidth = wordWidth + spaceWidth; // Start new line
                        measuredHeight += singleLineHeight; // Increase total height
                    }
                    else
                    {
                        currentLineWidth += wordWidth + spaceWidth;
                    }
                }

                maxLineWidth = Math.Max(maxLineWidth, currentLineWidth - spaceWidth); // remove trailing space
                measuredWidth = Math.Min(maxLineWidth, width);
            }

            // 4. Calculate Height
            if (heightMode == YGMeasureMode.Exactly)
            {
                measuredHeight = height;
            }
            else
            {
                // Add the height of the final (or single) line to our total
                measuredHeight += singleLineHeight;

                if (heightMode == YGMeasureMode.AtMost)
                {
                    measuredHeight = Math.Min(measuredHeight, height);
                }
            }

            return new YGSize
            {
                Width = (float)Math.Ceiling(measuredWidth),
                Height = (float)Math.Ceiling(measuredHeight),
            };
        }
    }
}
