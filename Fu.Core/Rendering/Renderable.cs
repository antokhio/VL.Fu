using Fu.Core.Interaction;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using VL.Skia;

namespace Fu.Core.Rendering
{
    public interface IRenderable : ILayer { }

    /// <summary>
    /// Closes the element chain: every node in a Fu tree is a <see cref="Renderable"/>,
    /// so <see cref="Tree.Traversable{TSelf}.Parent"/> and <see cref="Tree.Traversable{TSelf}.Children"/> are typed as such.
    /// </summary>
    public abstract class Renderable : Interactable<Renderable>, IRenderable
    {
        protected Renderable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        public RectangleF? Bounds => throw new NotImplementedException();

        public void Render(CallerInfo caller)
        {
            throw new NotImplementedException();
        }
    }
}
