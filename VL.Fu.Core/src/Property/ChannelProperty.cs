using VL.Lib.Reactive;

namespace VL.Fu.Core.Property
{
    /// <summary>
    /// A reactive property that combines the "set only if changed" logic of a CachedProperty
    /// with the broadcasting capabilities of an IChannel.
    /// </summary>
    /// <typeparam name="T">The type of the value to hold.</typeparam>
    public class ChannelProperty<T>
    {
        private readonly IChannel<T> _channel;

        /// <summary>
        /// The underlying reactive channel. Other parts of the system can subscribe to this to be notified of changes.
        /// </summary>
        public IChannel<T> Channel => _channel;

        /// <summary>
        /// Gets the current value of the property.
        /// </summary>
        public T Value => _channel.Value;

        /// <summary>
        /// Initializes a new instance of the ChannelProperty with an initial value.
        /// </summary>
        /// <param name="initialValue">The starting value for the property.</param>
        public ChannelProperty(T initialValue)
        {
            _channel = ChannelHelpers.CreateChannelOfType<T>();
            _channel.Value = initialValue;
        }

        /// <summary>
        /// Tries to set a new value. If the new value is different from the current value,
        /// it updates the property and pushes the change to the underlying channel.
        /// </summary>
        /// <param name="newValue">The new value to set.</param>
        /// <returns>True if the value was changed, false otherwise.</returns>
        public bool TrySetValue(T newValue)
        {
            // Use EqualityComparer<T>.Default to handle both structs (value types)
            // and classes (reference types) correctly.
            if (EqualityComparer<T>.Default.Equals(Value, newValue))
            {
                return false;
            }

            // If the value is different, update the channel.
            // This will automatically notify all subscribers.
            _channel.OnNext(newValue);

            return true;
        }
    }
}
