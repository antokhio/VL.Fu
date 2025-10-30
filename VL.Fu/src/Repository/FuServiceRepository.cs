namespace VL.Fu.Repository
{
    class FuServiceRepository
    {
        private readonly Dictionary<int, Dictionary<Type, WeakReference<object>>> _services = new();

        public void RegisterService<T>(T service, int hash)
            where T : class
        {
            if (!_services.TryGetValue(hash, out var typeDict))
            {
                typeDict = new Dictionary<Type, WeakReference<object>>();
                _services[hash] = typeDict;
            }
            typeDict[typeof(T)] = new WeakReference<object>(service);
        }

        public T GetService<T>(int hash)
            where T : class
        {
            if (_services.TryGetValue(hash, out var typeDict))
            {
                if (typeDict.TryGetValue(typeof(T), out var weakRef))
                {
                    if (weakRef.TryGetTarget(out var service))
                    {
                        return service as T;
                    }
                }
            }
            return null;
        }

        public void UnregisterService<T>(int hash)
            where T : class
        {
            if (_services.TryGetValue(hash, out var typeDict))
            {
                typeDict.Remove(typeof(T));
                if (typeDict.Count == 0)
                    _services.Remove(hash);
            }
        }

        public void UnregisterHash(int hash)
        {
            _services.Remove(hash);
        }
    }
}
