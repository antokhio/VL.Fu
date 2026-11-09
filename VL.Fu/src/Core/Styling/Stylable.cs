using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Tree;
using VL.Model;

namespace VL.Fu.Core.Styling
{
    public interface IStylable : ITraversable
    {
        /// <summary>
        /// The style tree this node was styled with last, or null when it has its default style.
        /// </summary>
        IStyle? Style { get; }

        /// <summary>
        /// Restyles the node from its defaults when <paramref name="style"/> is another reference than <see cref="Style"/>.
        /// Pass null to return every styled value to its default.
        /// Safe to call every frame: an unchanged reference costs one comparison.
        /// </summary>
        void SetStyle(IStyle? style);
    }

    /// <summary>
    /// First layer of the element chain. <typeparamref name="TSelf"/> is the node type of the tree,
    /// fixed once at the end of the chain, so every layer sees its parent and children fully typed.
    /// The Fu chain fixes it to <see cref="IFuNode"/>.
    /// </summary>
    /// <remarks>
    /// A styling pass resets the staged values of every layer to their defaults (<see cref="BeginStyle"/>),
    /// runs the style tree depth first, inputs before the instruction (<see cref="BuildStyleCore"/>),
    /// and commits what differs (<see cref="EndStyle"/>). So a removed or emptied setter falls back
    /// to the setter before it, or to the default.
    /// </remarks>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Stylable<TSelf> : Traversable<TSelf>, IStylable
        where TSelf : class, ITraversable<TSelf>, IStylable
    {
        private IStyle? _style;

        protected Stylable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public IStyle? Style => _style;

        [Fragment(Order = PinOrder.Style)]
        public void SetStyle(IStyle? style)
        {
            if (ReferenceEquals(_style, style))
                return;

            _style = style;

            BeginStyle();
            if (style is not null)
                Build(style);
            EndStyle();
        }

        /// <summary>
        /// Starts a styling pass: set every staged value of this layer to its default.
        /// Override for values a derived node type styles, and always call the base implementation.
        /// </summary>
        protected virtual void BeginStyle() { }

        /// <summary>
        /// Ends a styling pass: commit every staged value of this layer that differs from the last committed one.
        /// Override for values a derived node type styles, and always call the base implementation.
        /// </summary>
        protected virtual void EndStyle() { }

        /// <summary>
        /// Runs one instruction, without its inputs. Layers override this to process the instructions they own in bulk,
        /// like <see cref="YogaStyle"/> on layoutable nodes, and pass the rest to the base implementation.
        /// </summary>
        protected virtual void BuildStyleCore(IStyle instruction) => instruction.Build(this);

        // Inputs left to right, then the instruction itself, so later instructions win.
        private void Build(IStyle style)
        {
            var inputs = style.Inputs;
            for (var i = 0; i < inputs.Count; i++)
            {
                if (inputs[i] is { } input)
                    Build(input);
            }

            try
            {
                BuildStyleCore(style);
            }
            catch (Exception e)
            {
                this.LogWarning($"{style.GetType().Name} skipped: {e.Message}");
            }
        }
    }
}
