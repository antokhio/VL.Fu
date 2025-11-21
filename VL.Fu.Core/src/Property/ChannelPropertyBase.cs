using System.Collections.Immutable;
using VL.Core;
using VL.Lib.Collections;
using VL.Lib.Reactive;

namespace VL.Fu.Core.Property
{
    /// <summary>
    /// An abstract base class that implements IChannel T by wrapping an internal channel.
    /// This class handles the boilerplate of delegating all interface members to the internal channel.
    /// </summary>
    /// <typeparam name="T">The type of the value to hold.</typeparam>
    public abstract class ChannelPropertyBase<T> : IChannel<T>, IDisposable
    {
        protected readonly IChannel<T> _internalChannel;

        protected ChannelPropertyBase(IChannel<T> internalChannel)
        {
            _internalChannel = internalChannel;
        }

        public T? Value
        {
            get => _internalChannel.Value;
            set => _internalChannel.Value = value;
        }

        public void SetValueAndAuthor(T? value, string? author) =>
            _internalChannel.SetValueAndAuthor(value, author);

        public Func<T?, Optional<T?>>? Validator
        {
            set => _internalChannel.Validator = value;
        }

        // IChannel members
        public Type ClrTypeOfValues => _internalChannel.ClrTypeOfValues;
        public ImmutableArray<object> Components
        {
            get => _internalChannel.Components;
            set => _internalChannel.Components = value;
        }
        public IChannel<object> ChannelOfObject => _internalChannel.ChannelOfObject;
        public bool Enabled
        {
            get => _internalChannel.Enabled;
            set => _internalChannel.Enabled = value;
        }
        public bool IsBusy => _internalChannel.IsBusy;
        public object? Object
        {
            get => _internalChannel.Object;
            set => _internalChannel.Object = value;
        }
        public string? LatestAuthor => _internalChannel.LatestAuthor;

        public void SetObjectAndAuthor(object? @object, string? author) =>
            _internalChannel.SetObjectAndAuthor(@object, author);

        public IDisposable BeginChange() => _internalChannel.BeginChange();

        public string? Path => _internalChannel.Path;
        public int Revision => _internalChannel.Revision;
        public AccessorNodes AccessorNodes => _internalChannel.AccessorNodes;
        public bool IsInitialized => _internalChannel.IsInitialized;
        public bool HasBeenRequested => _internalChannel.HasBeenRequested;

        // Corrected IMonadicValue members
        public bool HasValue => _internalChannel.HasValue;
        public bool AcceptsValue => _internalChannel.AcceptsValue;

        IMonadicValue<T> IMonadicValue<T>.SetValue(T? value) =>
            ((IMonadicValue<T>)_internalChannel).SetValue(value);

        // ISubject<T> members
        public void OnCompleted() => _internalChannel.OnCompleted();

        public void OnError(Exception error) => _internalChannel.OnError(error);

        public void OnNext(T? value) => _internalChannel.OnNext(value);

        public IDisposable Subscribe(IObserver<T?> observer) =>
            _internalChannel.Subscribe(observer);

        // IHasAttributes members
        public Spread<Attribute> Attributes => _internalChannel.Attributes;

        private bool _disposed;

        public virtual void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _internalChannel.Dispose();
        }
    }
}
