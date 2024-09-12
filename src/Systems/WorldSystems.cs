using System.Collections.Generic;

namespace Wargon.Nukecs
{
    internal static class WorldSystems
    {
        private static readonly Dictionary<int, List<Systems>> systemsMap = new Dictionary<int, List<Systems>>();
    
        internal static void Add(int id, Systems systems)
        {
            if (!systemsMap.ContainsKey(id))
                systemsMap[id] = new List<Systems>();
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
    }
}