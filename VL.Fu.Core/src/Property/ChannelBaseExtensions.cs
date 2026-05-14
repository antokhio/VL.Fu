using System.Reactive.Linq;
using VL.Core;
using VL.Lib.Reactive;

namespace VL.Fu.Core.Property
{
    /// <summary>
    /// Provides explicit class-level extension methods for Channel{T} to bypass
    /// the compiler ambiguity between IChannel{T} and IChannel{object}.
    /// </summary>
    public static class ChannelBaseExtensions
    {
        // ====================================================================
        // Core Value Management
        // ====================================================================
        public static void EnsureValue<T>(
            this Channel<T> input,
            T? value,
            bool force = false,
            string? author = default
        )
        {
            ChannelHelpers.EnsureValue((IChannel<T>)input, value, force, author);
        }

        // ====================================================================
        // Merging Channels
        // ====================================================================

        public static IDisposable Merge<T>(
            this Channel<T> a,
            IChannel<T> b,
            ChannelMergeInitialization initialization,
            ChannelSelection pushEagerlyTo
        )
        {
            return ChannelHelpers.Merge((IChannel<T>)a, b, initialization, pushEagerlyTo);
        }

        public static IDisposable Merge<A, B>(
            this Channel<A> a,
            IChannel<B> b,
            Func<A?, B?> toB,
            Func<B?, A?> toA,
            ChannelMergeInitialization initialization,
            ChannelSelection pushEagerlyTo
        )
        {
            return ChannelHelpers.Merge((IChannel<A>)a, b, toB, toA, initialization, pushEagerlyTo);
        }

        public static IDisposable Merge<A, B>(
            this Channel<A> a,
            IChannel<B> b,
            Func<A?, Optional<B>> toB,
            Func<B?, Optional<A>> toA,
            ChannelMergeInitialization initialization,
            ChannelSelection pushEagerlyTo
        )
        {
            return ChannelHelpers.Merge((IChannel<A>)a, b, toB, toA, initialization, pushEagerlyTo);
        }

        // ====================================================================
        // Standard Reactive (Rx) Fixes
        // (These hide because Channel<T> implements both IObservable<T> and IObservable<object>)
        // ====================================================================

        public static void OnNext<T>(this Channel<T> channel, T? value, string? author = default)
        {
            ((IChannel<T>)channel).SetValueAndAuthor(value, author);
        }

        public static IDisposable Subscribe<T>(this Channel<T> channel, IObserver<T?> observer)
        {
            return ((IObservable<T?>)channel).Subscribe(observer);
        }

        public static IDisposable Subscribe<T>(this Channel<T> channel, Action<T?> onNext)
        {
            // Note: Requires System.Reactive.Linq or just using the standard Rx extensions
            return ((IObservable<T?>)channel).Subscribe(onNext);
        }

        public static IObservable<TResult> Select<T, TResult>(
            this Channel<T> channel,
            Func<T?, TResult> selector
        )
        {
            return ((IObservable<T?>)channel).Select(selector);
        }

        public static IObservable<T?> Where<T>(this Channel<T> channel, Func<T?, bool> predicate)
        {
            return ((IObservable<T?>)channel).Where(predicate);
        }

        public static int Revision<T>(this Channel<T> channel)
        {
            return ((IChannel<T>)channel).Revision;
        }
    }
}
