namespace VL.Fu.Core.Property
{
    /// <summary>
    /// A helper class that encapsulates a value and provides a mechanism to set it only if it has changed.
    /// This is useful for optimizing updates by avoiding redundant operations when a value remains the same across frames.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    public class CachedProperty<T>
    {
        private T _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedProperty{T}"/> class with an initial value.
        /// </summary>
        /// <param name="initialValue">The starting value for the property.</param>
        public CachedProperty(T initialValue)
        {
            _value = initialValue;
        }

        /// <summary>
        /// Attempts to set a new value for the property.
        /// The value is only updated if it is different from the current value, as determined by the default equality comparer for the type.
        /// </summary>
        /// <param name="newValue">The new value to set.</param>
        /// <param name="onValueChange">An optional action to execute if the value is changed. The action receives the old and new values.</param>
        /// <returns><c>true</c> if the value was changed; otherwise, <c>false</c>.</returns>
        public bool TrySetValue(T newValue, Action<T, T>? onValueChange = null)
        {
            // Use EqualityComparer<T>.Default to handle both structs (value types)
            // and classes (reference types) correctly.
            if (EqualityComparer<T>.Default.Equals(_value, newValue))
            {
                return false;
            }

            var oldValue = _value;
            _value = newValue;
            onValueChange?.Invoke(oldValue, newValue);

            return true;
        }

        /// <summary>
        /// Gets the current cached value.
        /// </summary>
        public T Value => _value;
    }
}
