using System.Collections.Generic;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Static registry mapping (worldId, typeIndex) to <see cref="IReactiveStorage"/>.
    /// Lives outside <see cref="World.WorldUnsafe"/> because the latter is an
    /// unmanaged struct that cannot hold managed references.
    /// </summary>
    internal static class ReactiveWorldRegistry
    {
        private static readonly Dictionary<(int world, int type), IReactiveStorage> ByKey = new();
        private static readonly Dictionary<int, List<IReactiveStorage>> PerWorld = new();
        private static readonly object Lock = new();

        public static ReactiveStorage<T> GetOrCreate<T>(World world) where T : unmanaged, IComponent
        {
            var typeIndex = ComponentType<T>.Index;
            var worldId = world.Id;
            lock (Lock)
            {
                if (ByKey.TryGetValue((worldId, typeIndex), out var existing))
                    return (ReactiveStorage<T>)existing;

                var storage = new ReactiveStorage<T>(world);
                ByKey[(worldId, typeIndex)] = storage;
                if (!PerWorld.TryGetValue(worldId, out var list))
                {
                    list = new List<IReactiveStorage>();
                    PerWorld[worldId] = list;
                }
                list.Add(storage);
                return storage;
            }
        }

        public static ReactiveStorage<T> Get<T>(World world) where T : unmanaged, IComponent
        {
            var typeIndex = ComponentType<T>.Index;
            var worldId = world.Id;
            lock (Lock)
            {
                return ByKey.TryGetValue((worldId, typeIndex), out var s)
                    ? (ReactiveStorage<T>)s
                    : null;
            }
        }

        public static bool TryGet<T>(World world, out ReactiveStorage<T> storage) where T : unmanaged, IComponent
        {
            storage = Get<T>(world);
            return storage != null;
        }

        /// <summary>Dispose all storages for the given world. Called on world free.</summary>
        public static void DisposeWorld(int worldId)
        {
            List<IReactiveStorage> list;
            lock (Lock)
            {
                if (!PerWorld.TryGetValue(worldId, out list)) return;
                PerWorld.Remove(worldId);
            }
            foreach (var s in list)
            {
                s.Dispose();
                ByKey.Remove((worldId, s.TypeIndex));
            }
        }

        /// <summary>
        /// Dispose ALL storages across ALL worlds. Called from <c>World.OnDisposeStatic</c>
        /// (e.g. between tests, on domain reload, on <c>World.DisposeStatic()</c>).
        /// </summary>
        public static void DisposeAll()
        {
            lock (Lock)
            {
                foreach (var kv in PerWorld)
                    foreach (var s in kv.Value) s.Dispose();
                PerWorld.Clear();
                ByKey.Clear();
            }
        }
    }
}
