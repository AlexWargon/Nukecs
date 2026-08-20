using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// User-facing API for per-entity reactive subscriptions.
    /// </summary>
    public static class EntityReactiveExtensions
    {
        /// <summary>
        /// Subscribe to changes of component <typeparamref name="T"/> on this entity.
        /// Callback fires next frame after the change is detected.
        /// Returns a token that can be passed to <see cref="OffChange{T}(Wargon.Nukecs.Entity,long)"/>.
        /// </summary>
        public static long OnChange<T>(this Entity entity, ReactDelegate<T> callback, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return Subscribe(entity, callback, options, null);
        }

        /// <summary>Subscribe with a filter predicate (skips dispatch when filter returns false).</summary>
        public static long OnChange<T>(this Entity entity, ReactDelegate<T> callback, ReactFilter<T> filter, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return Subscribe(entity, callback, options, filter);
        }

        /// <summary>Unsubscribe by the token returned from <c>OnChange</c>.</summary>
        public static void OffChange<T>(this Entity entity, long token) where T : unmanaged, IComponent
        {
            if (ReactiveStorageRegistry<T>.TryGet(entity.worldIndex, out var storage))
                storage.Remove(token);
        }

        /// <summary>Remove all subscriptions of type <typeparamref name="T"/> from this entity.</summary>
        public static void OffChange<T>(this Entity entity) where T : unmanaged, IComponent
        {
            if (ReactiveStorageRegistry<T>.TryGet(entity.worldIndex, out var storage))
                storage.RemoveAllForEntity(entity.id);
        }

        private static long Subscribe<T>(
            Entity entity,
            ReactDelegate<T> callback,
            ReactOptions options,
            ReactFilter<T> filter)
            where T : unmanaged, IComponent
        {
            ref var world = ref entity.world;
            SystemsReactiveExtensions.EnsureRegistered<T>(world);
            var storage = ReactiveStorageRegistry<T>.GetOrCreate(world);

            var sub = new Subscription<T> { Options = options, Managed = callback };
            if (filter != null) sub.SetManagedFilter(filter);

            var token = storage.AddEntitySubscription(entity.id, sub);

            // Bootstrap the oldValue snapshot so the first change doesn't false-positive.
            ref var ts = ref storage.TypeStateRef;
            if (entity.Has<T>())
            {
                if (!ts.Offsets.ContainsKey(entity.id))
                {
                    ref var current = ref entity.Get<T>();
                    unsafe
                    {
                        var newOffset = ts.AppendBytes((byte*)UnsafeUtility.AddressOf(ref current));
                        ts.Offsets.TryAdd(entity.id, newOffset);
                    }
                }
            }

            // TriggerImmediately: fire synchronously with current value when possible.
            // If T is not on the entity yet (deferred Add via ECB), defer the trigger —
            // the check system will enqueue the entity on first observation and dispatch
            // will fire the callback on the next OnUpdate (after ECB playback).
            if ((options & ReactOptions.TriggerImmediately) != 0)
            {
                if (entity.Has<T>())
                {
                    ref var v = ref entity.Get<T>();
                    sub.Managed?.Invoke(in v, in entity);
                }
                else
                {
                    sub.TriggerPending = true;
                    if (ts.PendingTriggers.IsCreated) ts.PendingTriggers[entity.id] = 1;
                }
            }

            return token;
        }
    }
}
