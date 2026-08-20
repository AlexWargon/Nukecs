using System.Collections.Generic;

namespace Wargon.Nukecs
{
    public static class WorldSystems
    {
        private static readonly Dictionary<int, System.Collections.Generic.List<Systems>> systemsMap = new Dictionary<int, System.Collections.Generic.List<Systems>>();
    
        internal static void Add(int id, Systems systems)
        {
            if (!systemsMap.ContainsKey(id))
                systemsMap[id] = new System.Collections.Generic.List<Systems>();
            systemsMap[id].Add(systems);
        }

        public static Systems Get(int world, int index)
        {
            return systemsMap[world][index];
        }
        public static List<Systems> GetAll(int worldId)
        {
            if (!systemsMap.ContainsKey(worldId))
                return new List<Systems>();
            return systemsMap[worldId];
        }
        internal static void CompleteAll(int id)
        {
            if(!systemsMap.ContainsKey(id)) return;
            var list = systemsMap[id];
            foreach (var systems in list)
            {
                systems.OnWorldDispose();
            }
        }

        internal static void Remove(int id)
        {
            if (systemsMap.ContainsKey(id))
                systemsMap.Remove(id);
        }

        public static void Dispose()
        {
            systemsMap.Clear();
        }
    }
}