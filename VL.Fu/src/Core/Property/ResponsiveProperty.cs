using Stride.Core.Mathematics;
using VL.Core;
using VL.Fu.Core.Common;
using VL.Fu.Core.Extensions;
using VL.Fu.Core.Input;
using VL.Skia;

namespace VL.Fu.Core.Property
{
    /// <summary>
    /// A property that maintains a value scaled to the current viewport space.
    /// It lazily connects to the ViewportStream to react to resolution/space changes.
    /// Input units are defined by the configured CommonSpace (defaulting to DIP).
    /// </summary>
    public abstract class ResponsiveProperty<T> : CachedProperty<T>, IDisposable
    {
        protected T _rawInput;
        protected CommonSpace _inputSpace;

        private readonly NodeContext _nodeContext;
        private readonly IContextProvider? _provider;

        private IDisposable? _viewportSubscription;
        private FuViewport? _lastViewport;

        // Constructor using NodeContext (Lookup required later)
        protected ResponsiveProperty(
            NodeContext nodeContext,
            T initialInput,
            CommonSpace inputSpace
        )
            : base(initialInput)
        {
            _rawInput = initialInput;
            _nodeContext = nodeContext;
            _inputSpace = inputSpace;
        }

        // Constructor using explicit Provider (No lookup required)
        protected ResponsiveProperty(
            IContextProvider provider,
            T initialInput,
            CommonSpace inputSpace
        )
            : base(initialInput)
        {
            _rawInput = initialInput;
            _provider = provider;
            _inputSpace = inputSpace;
        }

        /// <summary>
        /// Sets the input coordinate space.
        /// Triggers a recalculation of the current value.
        /// </summary>
        public void SetSpace(CommonSpace space = Constants.DefaultSpace)
        {
            if (_inputSpace == space)
                return;

            _inputSpace = space;

            // Trigger update with current input and new space
            TriggerUpdate();
        }

        /// <summary>
        /// Sets the raw input value.
        /// Triggers a recalculation and attempts to connect to the context if not yet connected.
        /// </summary>
        public void SetValue(T input)
        {
            _rawInput = input;
            TriggerUpdate();
        }

        private void TriggerUpdate()
        {
            EnsureSubscription();

            // If we have a valid viewport, recalculate immediately
            if (_lastViewport.HasValue)
            {
                UpdateValue(_rawInput, _lastViewport.Value);
            }
            else
            {
                // Fallback: if no context yet, just pass through the raw input.
                base.SetValue(_rawInput);
            }
        }

        private void EnsureSubscription()
        {
            if (_viewportSubscription != null)
                return;

            IContextProvider? provider = _provider;

            // If no direct provider, try to lookup via NodeContext
            if (provider == null)
            {
                FuProvider.TryGetProvider(_nodeContext, out provider);
            }

            if (provider != null)
            {
                if (provider.TryGetViewportStream(out var stream))
                {
                    _viewportSubscription = stream.Subscribe(OnViewportUpdate);
                }
            }
        }

        private void OnViewportUpdate(FuViewport viewport)
        {
            _lastViewport = viewport;
            UpdateValue(_rawInput, viewport);
        }

        private void UpdateValue(T input, FuViewport viewport)
        {
            var newValue = Calculate(input, viewport);
            base.SetValue(newValue);
        }

        protected abstract T Calculate(T input, FuViewport viewport);

        public void Dispose()
        {
            _viewportSubscription?.Dispose();
            _viewportSubscription = null;
        }
    }

    public class ResponsivePropertyFloat : ResponsiveProperty<float>
    {
        public ResponsivePropertyFloat(
            NodeContext nodeContext,
            float initialInput,
            CommonSpace inputSpace
        )
            : base(nodeContext, initialInput, inputSpace) { }

        public ResponsivePropertyFloat(
            IContextProvider provider,
            float initialInput,
            CommonSpace inputSpace
        )
            : base(provider, initialInput, inputSpace) { }

        protected override float Calculate(float input, FuViewport viewport)
        {
            return viewport.ConvertSpace(input, _inputSpace);
        }
    }

    /// <summary>
    /// Responsive property for Sizes or Deltas (e.g. Padding, Element Size).
    /// Does NOT respect coordinate system origin.
    /// </summary>
    public class ResponsivePropertySize : ResponsiveProperty<Vector2>
    {
        public ResponsivePropertySize(
            NodeContext nodeContext,
            Vector2 initialInput,
            CommonSpace inputSpace
        )
            : base(nodeContext, initialInput, inputSpace) { }

        public ResponsivePropertySize(
            IContextProvider provider,
            Vector2 initialInput,
            CommonSpace inputSpace
        )
            : base(provider, initialInput, inputSpace) { }

        protected override Vector2 Calculate(Vector2 input, FuViewport viewport)
        {
            return viewport.ConvertSize(input, _inputSpace);
        }
    }

    /// <summary>
    /// Responsive property for Positions (e.g. Anchors).
    /// Respects coordinate system origin (TopLeft vs Center).
    /// </summary>
    public class ResponsivePropertyPosition : ResponsiveProperty<Vector2>
    {
        public ResponsivePropertyPosition(
            NodeContext nodeContext,
            Vector2 initialInput,
            CommonSpace inputSpace
        )
            : base(nodeContext, initialInput, inputSpace) { }

        public ResponsivePropertyPosition(
            IContextProvider provider,
            Vector2 initialInput,
            CommonSpace inputSpace
        )
            : base(provider, initialInput, inputSpace) { }

        protected override Vector2 Calculate(Vector2 input, FuViewport viewport)
        {
            return viewport.ConvertPosition(input, _inputSpace);
        }
    }

    public class ResponsivePropertyRectangleF : ResponsiveProperty<RectangleF>
    {
        public ResponsivePropertyRectangleF(
            NodeContext nodeContext,
            RectangleF initialInput,
            CommonSpace inputSpace
        )
            : base(nodeContext, initialInput, inputSpace) { }

        public ResponsivePropertyRectangleF(
            IContextProvider provider,
            RectangleF initialInput,
            CommonSpace inputSpace
        )
            : base(provider, initialInput, inputSpace) { }

        protected override RectangleF Calculate(RectangleF input, FuViewport viewport)
        {
            return viewport.ConvertSpace(input, _inputSpace);
        }
    }
}
