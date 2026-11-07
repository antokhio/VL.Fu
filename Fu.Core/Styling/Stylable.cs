using Fu.Core.Base;
using Fu.Core.Tree;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace Fu.Core.Styling
{
    public interface IStylable
    {
        /// <summary>
        /// The style tree this node was styled with last, or null when it has its default style.
        /// </summary>
        Style? Style { get; }

        /// <summary>
        /// Restyles the node from its defaults when <paramref name="style"/> is another reference than <see cref="Style"/>.
        /// Pass null to return every styled value to its default.
        /// Safe to call every frame: an unchanged reference costs one comparison.
        /// </summary>
        void SetStyle(Style? style);
    }

    /// <summary>
    /// First layer of the element chain. <typeparamref name="TSelf"/> is the concrete node type of the tree,
    /// fixed once at the end of the chain, so every layer sees its parent and children fully typed.
    /// </summary>
    /// <remarks>
    /// A styling pass resets the staged values of every layer to their defaults (<see cref="BeginStyle"/>),
    /// runs the style tree depth first, inputs before the instruction (<see cref="BuildStyleCore"/>),
    /// and commits what differs (<see cref="EndStyle"/>). So a removed or emptied setter falls back
    /// to the setter before it, or to the default.
    /// </remarks>
    public abstract class Stylable<TSelf> : Traversable<TSelf>, IStylable
        where TSelf : Stylable<TSelf>
    {
        private Style? _style;

        protected Stylable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public Style? Style => _style;

        public void SetStyle(Style? style)
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
        protected virtual void BuildStyleCore(Style instruction) => instruction.Build(this);

        // Inputs left to right, then the instruction itself, so later instructions win.
        private void Build(Style style)
        {
            foreach (var input in style.Inputs)
            {
                if (input is not null)
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
