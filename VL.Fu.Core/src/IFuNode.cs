using VL.Skia;

namespace VL.Fu.Core
{
    public interface IFuNode : ITreeNode, ILayer, IInteractable, ILayoutable { }

    public interface IFuNodeInlay
    {
        void Evaluate(IFuNode node, out ILayer layer);
    }
}
