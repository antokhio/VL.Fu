using Fu.Core.Tree;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace Fu.Core.Styling
{
    public interface IStylable
    {
        IStyle Style { get; }
    }

    /// <summary>
    /// First layer of the element chain. <typeparamref name="TSelf"/> is the concrete node type of the tree,
    /// fixed once at the end of the chain, so every layer sees its parent and children fully typed.
    /// </summary>
    public abstract class Stylable<TSelf> : Traversable<TSelf>, IStylable
        where TSelf : Stylable<TSelf>
    {
        protected Stylable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public IStyle Style => throw new NotImplementedException();
    }
}
