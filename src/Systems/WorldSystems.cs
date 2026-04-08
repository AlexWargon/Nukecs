using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Wargon.Nukecs
{
    public static class WorldSystems
    {
        private static readonly ConcurrentDictionary<int, List<Systems>> systemsMap = new ConcurrentDictionary<int, List<Systems>>();
    
        internal static void Add(int id, Systems systems)
        {
            var list = systemsMap.GetOrAdd(id, _ => new List<Systems>());
            list.Add(systems);
        }

        public static Systems Get(int world, int index)
        {
            return systemsMap[world][index];
        }
        internal static void CompleteAll(int id)
        {
            if(!systemsMap.TryGetValue(id, out var list)) return;
            foreach (var systems in list)
            {
                systems.OnWorldDispose();
            }
        }

        public static void Dispose()
        {
            systemsMap.Clear();
        }
    }
}