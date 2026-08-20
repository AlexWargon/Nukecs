using System;

namespace Wargon.Nukecs.Reactivity
{
    [Flags]
    public enum ReactOptions : byte
    {
        None = 0,
        /// <summary>Auto-unsubscribe after first dispatch.</summary>
        Once = 1,
        /// <summary>Invoke the callback synchronously on subscribe with the current value.</summary>
        TriggerImmediately = 2,
    }

    /// <summary>
    /// One reactive subscription. Holds a managed <see cref="ReactDelegate{T}"/>
    /// and an optional filter. A subscription is identified by its <see cref="Token"/>
    /// (returned to the user for <c>OffChange</c>).
    /// </summary>
    internal sealed class Subscription<T> : IDisposable where T : unmanaged, IComponent
    {
        public long Token;
        public int EntityId;
        public ReactDelegate<T> Managed;
        public ReactFilter<T> ManagedFilter;
        public ReactOptions Options;

        /// <summary>
        /// Set when <see cref="ReactOptions.TriggerImmediately"/> was requested at subscribe
        /// time but the component was not yet on the entity (deferred Add via ECB).
        /// The check system consumes this on first observation of T and enqueues
        /// the entity for dispatch, so the initial trigger fires on the next OnUpdate
        /// (after ECB playback) instead of synchronously.
        /// </summary>
        public bool TriggerPending;

        public bool IsOnce => (Options & ReactOptions.Once) != 0;

        public void SetManagedFilter(ReactFilter<T> filter)
        {
            ManagedFilter = filter;
        }

        public void Dispose()
        {
            Managed = null;
            ManagedFilter = null;
            TriggerPending = false;
        }
    }
}
