using System.Collections.Generic;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Статический реестр <see cref="ReactiveWorldState"/> по worldId. Подписывается на
    /// <see cref="World.OnDisposeStatic"/> для очистки кэшей между сессиями (тесты,
    /// перезагрузка домена). Public — нужен для source-generated code.
    /// </summary>
    public static class ReactiveWorldRegistry
    {
        private static readonly Dictionary<int, ReactiveWorldState> ByWorldId = new();
        private static readonly object Lock = new();
        private static bool _staticCleanupHooked;

        public static ReactiveWorldState GetOrCreate(World world)
        {
            HookStaticCleanup();
            var worldId = world.Id;
            lock (Lock)
            {
                if (!ByWorldId.TryGetValue(worldId, out var state))
                {
                    state = new ReactiveWorldState();
                    state.Initialize();
                    ByWorldId[worldId] = state;
                }
                return state;
            }
        }

        public static bool TryGet(int worldId, out ReactiveWorldState state)
        {
            lock (Lock)
            {
                return ByWorldId.TryGetValue(worldId, out state);
            }
        }

        public static void DisposeWorld(int worldId)
        {
            ReactiveWorldState state;
            lock (Lock)
            {
                if (!ByWorldId.TryGetValue(worldId, out state)) return;
                ByWorldId.Remove(worldId);
            }
            state.Dispose();
        }

        public static void DisposeAll()
        {
            List<ReactiveWorldState> snapshot;
            lock (Lock)
            {
                snapshot = new List<ReactiveWorldState>(ByWorldId.Values);
                ByWorldId.Clear();
            }
            foreach (var s in snapshot) s.Dispose();
        }

        private static void HookStaticCleanup()
        {
            if (_staticCleanupHooked) return;
            _staticCleanupHooked = true;
            World.OnDisposeStatic(StaticCleanup);
        }

        private static void StaticCleanup()
        {
            DisposeAll();
            // World.DisposeStatic очищает свое поле события — разрешаем повторную привязку (re-hook) при следующем доступе.
            _staticCleanupHooked = false;
        }
    }
}
