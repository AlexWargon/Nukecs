using System.Collections.Generic;

namespace Wargon.Nukecs.Reactivity
{
    public static class SystemsReactiveExtensions
    {
        private static readonly HashSet<Systems> HookedSystems = new();
        private static bool _staticCleanupHooked;

        /// <summary>
        /// Explicitly register the non-generic check system and per-T dispatch system
        /// for this world. Auto-registration also happens on first <c>OnChange&lt;T&gt;</c>,
        /// so calling this manually is optional. Safe to call multiple times.
        /// </summary>
        public static Systems AddReactive<T>(this Systems systems) where T : unmanaged, IComponent
        {
            EnsureRegistered<T>(systems.World);
            return systems;
        }

        /// <summary>
        /// Ensure <see cref="ReactiveCheckSystem"/> (once per Systems) and
        /// <see cref="ReactDispatchSystem{T}"/> (per type) are registered.
        /// </summary>
        public static void EnsureRegistered<T>(World world) where T : unmanaged, IComponent
        {
            HookStaticCleanup();

            ReactiveStorageRegistry<T>.GetOrCreate(world);

            var systemsList = WorldSystems.GetAll(world.Id);
            foreach (var systems in systemsList)
            {
                // Check FIRST — schedules Burst job, stores handle in ReactiveJobSync.
                if (HookedSystems.Add(systems))
                {
                    systems.Add<ReactiveCheckSystem>(0);
                    systems.onWorldDispose += OnDisposeWorld;
                }
                // Dispatch SECOND — waits on check job ONLY (via ReactiveJobSync),
                // reads ChangedQueue, invokes callbacks on main thread.
                if (!DispatchRegistered<T>.IsRegistered(systems))
                {
                    DispatchRegistered<T>.MarkRegistered(systems);
                    systems.Add<ReactDispatchSystem<T>>(0);
                }
            }
        }

        private static void OnDisposeWorld(ref World w)
        {
            ReactiveWorldRegistry.DisposeWorld(w.Id);
            var list = WorldSystems.GetAll(w.Id);
            foreach (var s in list) HookedSystems.Remove(s);
        }

        private static void HookStaticCleanup()
        {
            if (_staticCleanupHooked) return;
            _staticCleanupHooked = true;
            World.OnDisposeStatic(StaticCleanup);
        }

        private static void StaticCleanup()
        {
            ReactiveWorldRegistry.DisposeAll();
            ReactiveStorageAll.DisposeAll();
            HookedSystems.Clear();
            _staticCleanupHooked = false;
        }
    }

    /// <summary>Per-T marker: "ReactDispatchSystem&lt;T&gt; already registered".</summary>
    internal static class DispatchRegistered<T> where T : unmanaged, IComponent
    {
        private static readonly HashSet<Systems> Set = new();
        private static bool _cleanupHooked;

        public static bool IsRegistered(Systems systems) => Set.Contains(systems);
        public static void MarkRegistered(Systems systems)
        {
            Set.Add(systems);
            if (!_cleanupHooked)
            {
                _cleanupHooked = true;
                World.OnDisposeStatic(ClearAll);
            }
        }

        public static void ClearAll()
        {
            Set.Clear();
            _cleanupHooked = false;
        }
    }
}
