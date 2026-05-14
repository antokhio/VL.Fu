using VL.Core;
using VL.Core.Import;

namespace VL.Fu.Core
{
    public interface IStylable : ILayoutable { }

    [ProcessNode]
    public abstract class Stylable : Layoutable, IStylable
    {
        private IFuStyle? _style;

        public void SetStyle(IFuStyle? style)
        {
            if (ReferenceEquals(_style, style))
                return;

            _style = style;

            if (_style != null)
                _style.Apply(this);
        }

        protected Stylable(NodeContext nodeContext)
            : base(nodeContext) { }
    }
}
