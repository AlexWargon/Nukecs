using System.Collections.Generic;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public static class EntityPrefabMap {
        private static Dictionary<int, Entity> Map = new Dictionary<int, Entity>();

        public static void Clear() {
            Map.Clear();
        }
        public static void Add(int id, Entity entity) {
            if (Map.ContainsKey(id)) return;
            Map[id] = entity;
        }

        public static Entity Get(int id) {
            var prefab = Map[id];
            return Map[id].world.SpawnPrefab(in prefab);
        }
        public static Entity GetPrefab(int id) {
            return Map[id];
        }
        public static Entity GetOrCreatePrefab<T>(T obj, ref World world) where T : Object, ICustomConvertor {
            var id = obj.GetInstanceID();
            if (!Map.ContainsKey(id)) {
                var e = world.Entity();
                obj.Convert(ref world, ref e);
                e.Add(new IsPrefab());
                Map[id] = e;
                return e;
            }

            return Map[id];
        }
        public static bool TryGet(int id, out Entity entity) {
            if (Map.ContainsKey(id)) {
                var prefab = Map[id];
                entity = Map[id].world.SpawnPrefab(in prefab);
                return true;
            }
            entity = Entity.Null;
            return false;
        }
    }
}