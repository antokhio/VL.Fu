using VL.Core.Import;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO;

namespace VL.Fu.Interaction.Gestures
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class KeyPressGesture : KeyGesture
    {
        public KeyPressGesture(IFuBehaviour behaviour, Keys key, Keys modifiers = Keys.None)
            : base(behaviour, key, modifiers) { }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            bool match = CheckConditions(inputState);

            if (Status == GestureStatus.Idle)
            {
                if (match)
                    Status = GestureStatus.Start;
            }
            else if (Status == GestureStatus.Start || Status == GestureStatus.Update)
            {
                if (match)
                    Status = GestureStatus.Update; // Held down
                else
                    Status = GestureStatus.Finish; // Released
            }
            else
            {
                if (!match)
                    Status = GestureStatus.Idle; // Reset
            }
        }
    }
}
