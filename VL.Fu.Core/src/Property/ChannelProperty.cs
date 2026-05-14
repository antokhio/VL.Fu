using VL.Lib.Reactive;

namespace VL.Fu.Core.Property
{
    public class ChannelProperty<T> : ChannelBase<T>, IChannel<T>
    {
        public ChannelProperty() { }

        public ChannelProperty(T? value)
        {
            this.Value = value;
        }

        private IChannel<T>? _boundChannel;
        private IDisposable _bindingSubscription;

        public void SetChannel(IChannel<T> channel)
        {
            if (ReferenceEquals(_boundChannel, channel))
                return;

            _boundChannel = channel;
            _bindingSubscription?.Dispose();

            if (_boundChannel != null)
            {
                _bindingSubscription = this.Merge(
                    _boundChannel,
                    ChannelMergeInitialization.UseB,
                    ChannelSelection.Both
                );
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
