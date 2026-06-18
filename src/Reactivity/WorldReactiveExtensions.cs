namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// World-level reactive subscriptions: callbacks fire for any entity whose
    /// component <typeparamref name="T"/> changes (no need to subscribe per-entity).
    /// </summary>
    public static class WorldReactiveExtensions
    {
        /// <summary>
        /// Subscribe a callback that fires whenever component <typeparamref name="T"/>
        /// changes on any entity in this world. Returns a token for
        /// <see cref="OffChange{T}(Wargon.Nukecs.World,long)"/>.
        /// </summary>
        public static long OnChange<T>(this World world, ReactDelegate<T> callback, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return WorldSubscribe(world, callback, null, options, null, null);
        }

        /// <summary>World-level subscribe with a filter predicate.</summary>
        public static long OnChange<T>(this World world, ReactDelegate<T> callback, ReactFilter<T> filter, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return WorldSubscribe(world, callback, null, options, filter, null);
        }

        /// <summary>
        /// World-level Burst callback (non-generic signature, receives only the entity).
        /// <paramref name="burstCallback"/> must be marked
        /// <c>[BurstCompile] [AOT.MonoPInvokeCallback(typeof(ReactDelegateBurst))]</c>.
        /// </summary>
        public static long OnChangeBurst<T>(this World world, ReactDelegateBurst burstCallback, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return WorldSubscribe<T>(world, null, burstCallback, options | ReactOptions.IsBurst, null, null);
        }

        /// <summary>World-level Burst callback + burst filter.</summary>
        public static long OnChangeBurst<T>(this World world, ReactDelegateBurst burstCallback, ReactFilterBurst burstFilter, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return WorldSubscribe<T>(world, null, burstCallback, options | ReactOptions.IsBurst, null, burstFilter);
        }

        /// <summary>Unsubscribe a world-level subscription by token.</summary>
        public static void OffChange<T>(this World world, long token) where T : unmanaged, IComponent
        {
            if (ReactiveWorldRegistry.TryGet<T>(world, out var storage))
                storage.Remove(token);
        }

        private static long WorldSubscribe<T>(
            World world,
            ReactDelegate<T> managed,
            ReactDelegateBurst burstDelegate,
            ReactOptions options,
            ReactFilter<T> managedFilter,
            ReactFilterBurst burstFilter)
            where T : unmanaged, IComponent
        {
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

            return storage.AddWorldSubscription(sub);
        }
    }
}
