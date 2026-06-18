using System.Collections.Generic;

namespace Wargon.Nukecs.Reactivity
{
    public static class SystemsReactiveExtensions
    {
        private static readonly HashSet<Systems> HookedSystems = new();
        private static bool _staticCleanupHooked;

        /// <summary>
        /// Explicitly register the three reactive systems (Check/BurstDispatch/ManagedDispatch)
        /// for component type <typeparamref name="T"/> on this world. Auto-registration also
        /// happens on the first call to <c>OnChange&lt;T&gt;</c>, so calling this manually is optional.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        public static Systems AddReactive<T>(this Systems systems) where T : unmanaged, IComponent
        {
            EnsureRegistered<T>(systems.World);
            return systems;
        }

        /// <summary>
        /// Register reactive systems for <typeparamref name="T"/> against every
        /// <see cref="Systems"/> instance attached to the given world. Idempotent.
        /// </summary>
        internal static void EnsureRegistered<T>(World world) where T : unmanaged, IComponent
        {
            HookStaticCleanup();

            var storage = ReactiveWorldRegistry.GetOrCreate<T>(world);
            if (storage.SystemsRegistered) return;

            var list = WorldSystems.GetAll(world.Id);
            if (list.Count == 0)
            {
                // No Systems instance yet — defer registration until one is created
                // (a subsequent OnChange call will retry).
                return;
            }

            storage.SystemsRegistered = true;
            foreach (var systems in list)
            {
                // Pass explicit int dummy to force the `Add<T>(int) where T : struct, ISystem` overload.
                systems.Add<ReactiveCheckSystem<T>>(0);
                systems.Add<ReactBurstDispatchSystem<T>>(0);
                systems.Add<ReactManagedDispatchSystem<T>>(0);

                if (HookedSystems.Add(systems))
                    systems.onWorldDispose += OnDisposeWorld;
            }
        }

        private static void HookStaticCleanup()
        {
            // World.DisposeStatic() wipes its event field each call (`OnDisposeStaticEvent = null`),
            // so re-subscribe on every EnsureRegistered call if we may have been unsubscribed.
            // Tracking flag is reset inside StaticCleanup so we re-hook after each wipe.
            if (_staticCleanupHooked) return;
            _staticCleanupHooked = true;
            World.OnDisposeStatic(StaticCleanup);
        }

        private static void StaticCleanup()
        {
            ReactiveWorldRegistry.DisposeAll();
            HookedSystems.Clear();
            // Allow re-hook on next EnsureRegistered — World.DisposeStatic wiped the event.
            _staticCleanupHooked = false;
        }

        private static void OnDisposeWorld(ref World w)
        {
            var worldId = w.Id;
            ReactiveWorldRegistry.DisposeWorld(worldId);
            // Drop dispose hooks for Systems instances of this world (otherwise we'd
            // leak them in the static set; CompleteAll runs before WorldSystems.Remove).
            var list = WorldSystems.GetAll(worldId);
            foreach (var s in list) HookedSystems.Remove(s);
        }
    }
}
