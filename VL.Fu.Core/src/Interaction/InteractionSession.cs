using VL.Fu.Core.Interaction;

namespace VL.Fu.Core.Services
{
    /// <summary>
    /// Represents a live interaction session that has successfully matched and is currently running.
    /// Keeps track of the host, behavior, gesture, and captured input resources.
    /// </summary>
    public class InteractionSession
    {
        public IFuBehaviour Behaviour { get; }
        public IFuGesture Gesture { get; }
        public IFuNode Host { get; }

        // Pointers claimed by this interaction.
        private readonly HashSet<int> _capturedPointerIds = new();
        public IReadOnlySet<int> CapturedPointerIds => _capturedPointerIds;

        // Updated to use the new GesturePhase enum from the standardized pipeline
        public bool IsFinished =>
            Gesture.Phase == GesturePhase.Matched
            || Gesture.Phase == GesturePhase.Failed
            || Gesture.Phase == GesturePhase.Cancelled;

        public InteractionSession(IFuBehaviour behaviour, IFuGesture gesture, IFuNode host)
        {
            Behaviour = behaviour;
            Gesture = gesture;
            Host = host;
        }

        public void CapturePointer(int pointerId)
        {
            _capturedPointerIds.Add(pointerId);
        }

        public void ReleasePointer(int pointerId)
        {
            _capturedPointerIds.Remove(pointerId);
        }

        public bool IsCapturing(int pointerId) => _capturedPointerIds.Contains(pointerId);
    }
}
