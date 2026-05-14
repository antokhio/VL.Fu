using VL.Core.Import;
using VL.Fu.Core;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Base class for style setter nodes.
    /// </summary>
    [ProcessNode]
    public abstract class StyleNode
    {
        protected IFuStyle? Input { get; private set; }
        public IFuStyle Output { get; protected set; }

        private readonly Action<IStylable> _applyAction;

        public StyleNode()
        {
            _applyAction = Apply;

            Build();
        }

        public void SetInput(IFuStyle input)
        {
            if (ReferenceEquals(Input, input))
                return;

            Input = input;

            Build();
        }

        /// <summary>
        /// Applies style
        /// </summary>
        protected abstract void Apply(IStylable node);

        /// <summary>
        /// Builds chain
        /// </summary>
        protected virtual void Build()
        {
            Output = new FuStyle(Input, _applyAction);
        }

        /// <summary>
        /// Cachable setter for properties.
        /// </summary>
        protected void SetValue<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            Build();
        }
    }
}
