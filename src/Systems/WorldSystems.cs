using System.Collections.Generic;

namespace Wargon.Nukecs
{
    internal static class WorldSystems
    {
        private static readonly Dictionary<int, System.Collections.Generic.List<Systems>> systemsMap = new Dictionary<int, System.Collections.Generic.List<Systems>>();
    
        internal static void Add(int id, Systems systems)
        {
            if (!systemsMap.ContainsKey(id))
                systemsMap[id] = new System.Collections.Generic.List<Systems>();
            systemsMap[id].Add(systems);
        }

        internal static void CompleteAll(int id)
        {
            var list = systemsMap[id];
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