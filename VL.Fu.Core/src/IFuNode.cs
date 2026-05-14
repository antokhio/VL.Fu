using VL.Skia;

namespace VL.Fu.Core
{
    public interface IFuNode : ITreeNode, ILayer, IInteractable, ILayoutable, IMeasurable { }

    public interface IFuNodeInlay
    {
        void Evaluate(IFuNode node, out ILayer layer);
    }
}
