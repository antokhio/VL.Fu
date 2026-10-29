using Fu.Interaction.Behavior;
using VL.Lib.Collections;

namespace Fu.Interaction.Interactable
{
    public interface IInteractable
    {
        public bool IsEnabled { get; }
    }

    public abstract class Interactable : IInteractable
    {
        public bool IsEnabled { get; set; } = true;

        public IReadOnlyList<IInteractionBehavior> Behaviors { get; set; } =
            Spread<IInteractionBehavior>.Empty;
    }
}
