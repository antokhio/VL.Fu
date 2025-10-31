namespace VL.Fu.Core.Helpers
{
    public class CachedProperty<T>
    {
        private T _value;

        public CachedProperty(T initialValue)
        {
            _value = initialValue;
        }

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

        public T Value => _value;
    }
}
