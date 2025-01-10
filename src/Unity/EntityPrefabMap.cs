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
            // if (Map.TryAdd(id, entity))
            // {
            //     entity.Add(new IsPrefab());
            //     ref var children = ref entity.TryGet<ComponentArray<Child>>(out var exist);
            //     if (exist)
            //     {
            //         foreach (ref var child in children)
            //         {
            //             child.Value.Add(new IsPrefab());
            //         }
            //     }
            // }
        }

        public static Entity Spawn(int id) {
            var prefab = Map[id];
            return Map[id].world.SpawnPrefab(in prefab);
        }
        public static Entity Spawn<T>(T obj, ref World world) where T : Object, ICustomConvertor {
            var prefab = GetOrCreatePrefab(obj, ref world);
            return prefab.world.SpawnPrefab(in prefab);
        }
        public static Entity GetPrefab(int id) {
            return Map[id];
        }
        public static Entity GetOrCreatePrefab<T>(T obj, ref World world) where T : Object, ICustomConvertor {
            var id = obj.GetInstanceID();
            if (!Map.TryGetValue(id, out var prefab)) {
                var e = world.Entity();
                obj.Convert(ref world, ref e);
                e.Add(new IsPrefab());
                Map[id] = e;
                return e;
            }

            return prefab;
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