using System;
using Wargon.Nukecs;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// User-facing API for per-entity reactive subscriptions.
    /// </summary>
    public static class EntityReactiveExtensions
    {
        // ============ Managed callbacks ============

        /// <summary>
        /// Subscribe to changes of component <typeparamref name="T"/> on this entity.
        /// Callback fires next frame after the change is detected.
        /// Returns a token that can be passed to <see cref="OffChange{T}(Wargon.Nukecs.Entity,long)"/>.
        /// </summary>
        public static long OnChange<T>(this Entity entity, ReactDelegate<T> callback, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return Subscribe(entity, callback, null, options, null, null);
        }

        /// <summary>Subscribe with a filter predicate (skips dispatch when filter returns false).</summary>
        public static long OnChange<T>(this Entity entity, ReactDelegate<T> callback, ReactFilter<T> filter, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return Subscribe(entity, callback, null, options, filter, null);
        }

        // ============ Burst callbacks (function-pointer) ============

        /// <summary>
        /// Subscribe a Burst-compiled static method. The callback receives only the
        /// entity (non-generic delegate signature — required for FunctionPointer
        /// invocation under Mono). Read the component inside via <c>entity.Get&lt;T&gt;()</c>.
        ///
        /// The callback must be marked with
        /// <c>[BurstCompile] [AOT.MonoPInvokeCallback(typeof(ReactDelegateBurst))]</c>.
        /// </summary>
        public static long OnChangeBurst<T>(this Entity entity, ReactDelegateBurst burstCallback, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return Subscribe<T>(entity, null, burstCallback, options | ReactOptions.IsBurst, null, null);
        }

        /// <summary>Burst callback + burst filter (both must be Burst-compiled static methods).</summary>
        public static long OnChangeBurst<T>(this Entity entity, ReactDelegateBurst burstCallback, ReactFilterBurst burstFilter, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return Subscribe<T>(entity, null, burstCallback, options | ReactOptions.IsBurst, null, burstFilter);
        }

        // ============ Unsubscribe ============

        /// <summary>Unsubscribe by the token returned from <c>OnChange*</c>.</summary>
        public static void OffChange<T>(this Entity entity, long token) where T : unmanaged, IComponent
        {
            if (ReactiveWorldRegistry.TryGet<T>(entity.world, out var storage))
                storage.Remove(token);
        }

        /// <summary>Remove all subscriptions of type <typeparamref name="T"/> from this entity.</summary>
        public static void OffChange<T>(this Entity entity) where T : unmanaged, IComponent
        {
            if (ReactiveWorldRegistry.TryGet<T>(entity.world, out var storage))
                storage.RemoveAllForEntity(entity.id);
        }

        // ============ Internals ============
        private static long Subscribe<T>(
            Entity entity,
            ReactDelegate<T> managed,
            ReactDelegateBurst burstDelegate,
            ReactOptions options,
            ReactFilter<T> managedFilter,
            ReactFilterBurst burstFilter)
            where T : unmanaged, IComponent
        {
            ref var world = ref entity.world;
            SystemsReactiveExtensions.EnsureRegistered<T>(world);
            var storage = ReactiveWorldRegistry.GetOrCreate<T>(world);

            var sub = new Subscription<T> { Options = options };

            if (burstDelegate != null)
            {
                sub.SetBurst(burstDelegate);
                if (burstFilter != null) sub.SetBurstFilter(burstFilter);
            }
            else
            {
                sub.Managed = managed;
                if (managedFilter != null) sub.SetManagedFilter(managedFilter);
            }

            var token = storage.AddEntitySubscription(entity.id, sub);

            // Bootstrap OldValues with current value so first change doesn't false-positive.
            if (entity.Has<T>())
                storage.OldValues.TryAdd(entity.id, entity.Get<T>());

            // TriggerImmediately: fire synchronously with current value when possible.
            // If T is not on the entity yet (deferred Add via ECB), defer the trigger —
            // the check system will enqueue the entity on first observation and dispatch
            // will fire the callback on the next OnUpdate (after ECB playback).
            if ((options & ReactOptions.TriggerImmediately) != 0)
            {
                if (entity.Has<T>())
                {
                    // Burst sub: FunctionPointer<ReactDelegateBurst>.Invoke (non-generic, works).
                    // Managed sub: invoke via delegate.
                    if (sub.IsBurst && sub.BurstFnPtr != IntPtr.Zero)
                    {
                        var fp = new Unity.Burst.FunctionPointer<ReactDelegateBurst>(sub.BurstFnPtr);
                        if (fp.IsCreated) fp.Invoke(in entity);
                    }
                    else
                    {
                        sub.Managed?.Invoke(in entity.Get<T>(), in entity);
                    }
                }
                else
                {
                    // Component not on entity yet — defer until first observation.
                    sub.TriggerPending = true;
                    storage.SetPendingTrigger(entity.id);
                }
            }

            return token;
        }
    }
}
