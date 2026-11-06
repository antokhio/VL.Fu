using Fu.Core.Style;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace Fu.Core.Layout
{
    public interface ILayoutable
    {
        RectangleF Layout { get; }
    }

    public abstract class Layoutable<TSelf> : Stylable<TSelf>, ILayoutable
        where TSelf : Layoutable<TSelf>
    {
        protected Layoutable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public RectangleF Layout => throw new NotImplementedException();
    }
}
