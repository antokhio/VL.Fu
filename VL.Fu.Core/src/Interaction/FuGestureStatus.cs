namespace VL.Fu.Core.Interaction
{
    public enum GestureStatus
    {
        Idle, // Not active
        Possible, // Conditions met (e.g. Hovering, MouseDown), waiting for logic (threshold) or priority
        Start, // Transition: Logic decided to begin (e.g. threshold passed)
        Update, // Running state
        Finish, // Transition: Completed successfully
        Cancel, // Transition: Aborted (by logic or external priority conflict)
    }
}
