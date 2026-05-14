using VL.Lib.Reactive;

namespace VL.Fu.Core.Property
{
    public abstract class ChannelBase<T> : Channel<T>, IChannel<T>, IChannel, IDisposable
    {
        private readonly Action _baseDispose;

        public ChannelBase()
        {
            var map = typeof(C<T>).GetInterfaceMap(typeof(IDisposable));
            var methodInfo = map.TargetMethods[0];

            _baseDispose = (Action)Delegate.CreateDelegate(typeof(Action), this, methodInfo);
        }

        public ChannelBase(T? value)
            : this()
        {
            Value = value;
        }

        public virtual void Dispose()
        {
            _baseDispose?.Invoke();

            GC.SuppressFinalize(this);
        }
    }
}
