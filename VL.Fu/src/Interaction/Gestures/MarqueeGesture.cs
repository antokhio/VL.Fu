using Stride.Core.Mathematics;
using VL.Fu.Core.Common;
using VL.Fu.Core.Input;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Interaction.Gestures
{
    public class MarqueeGesture : GestureBase
    {
        public override int Priority => GesturePriority.MarqueeSelection;

        public Vector2 StartPosition { get; private set; }
        public Vector2 CurrentPosition { get; private set; }

        private const float Threshold = 0.01f;

        public MarqueeGesture(IFuBehaviour behaviour)
            : base(behaviour) { }

        public override void Evaluate(FuInputState inputState, IEnumerable<FuPointer> candidates)
        {
            foreach (var pointer in candidates)
            {
                if (pointer.State == TouchNotificationKind.TouchDown && !_activators.Any())
                {
                    _activators.Add(pointer);
                    StartPosition = pointer.Position;
                    CurrentPosition = pointer.Position;
                    Status = GestureStatus.Possible;
                }
            }

            if (_activators.Count > 0)
            {
                var primary = _activators[0];
                if (inputState.Pointers.TryGetValue(primary.Id, out var curr))
                {
                    _activators[0] = curr;
                    CurrentPosition = curr.Position;

                    if (curr.State == TouchNotificationKind.TouchUp)
                    {
                        Status =
                            (Status == GestureStatus.Start || Status == GestureStatus.Update)
                                ? GestureStatus.Finish
                                : GestureStatus.Cancel;
                        _activators.Clear();
                    }
                    else if (
                        Status == GestureStatus.Possible
                        && (CurrentPosition - StartPosition).Length() > Threshold
                    )
                    {
                        Status = GestureStatus.Start;
                    }
                    else if (Status == GestureStatus.Start)
                    {
                        Status = GestureStatus.Update;
                    }
                }
                else
                {
                    Status = GestureStatus.Cancel;
                    _activators.Clear();
                }
            }
            else if (Status != GestureStatus.Finish && Status != GestureStatus.Cancel)
            {
                Status = GestureStatus.Idle;
            }
        }
    }
}
