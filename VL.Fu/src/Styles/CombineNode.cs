using Fu.Core.Styling;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Joins styles into one, later inputs winning over earlier ones. Its output can be the input of another
    /// combine or setter, so styles form a tree that can share subtrees.
    /// </summary>
    [ProcessNode(Name = "Combine")]
    public sealed class CombineNode : StyleNode
    {
        public CombineNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) => null;
    }
}
